// See https://aka.ms/new-console-template for more information
using System.Text;
using System.Reflection;
using utauPlugin;

using DsPitch;

Console.WriteLine($"DsPitch {Assembly.GetEntryAssembly()?.GetName().Version}");
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var arg = Environment.GetCommandLineArgs();

//Load tmp ust file
if (arg.Length < 2)
{
    Console.WriteLine("Please launch this plugin from utau editor");
    Console.ReadLine();
    return;
}
UtauPlugin plugin = new UtauPlugin(arg[1], Encoding.UTF8);
plugin.Input();

//prepare notes and timeaxis
var uNotes = UtauUtil.PluginNotesToUNotes(plugin);
var tempos = UtauUtil.GetTempoList(plugin, uNotes);
var timeAxis = new TimeAxis();
timeAxis.BuildSegments(tempos);
var phrases = UtauUtil.UNotesToPhrases(uNotes);

//Load .ds file
Console.WriteLine("Please input the location of your .ds file");
string dsPath = Console.ReadLine().Trim('\"');
var dsScript = DiffSingerScript.Load(dsPath);

foreach(var dsPhrase in dsScript){
    var f0_seq = dsPhrase.f0_seq;
    if (f0_seq == null)
    {
        continue;
    }
    var f0_timestep = dsPhrase.f0_timestep;
    var f0_timestep_ms = f0_timestep * 1000;

    //time layout
    var offset = dsPhrase.offset;
    var offset_ms = offset*1000;
    var end_ms = offset_ms + f0_seq.Length*f0_timestep_ms;
    var offset_tick = timeAxis.MsPosToTickPos(offset_ms);
    var end_tick = timeAxis.MsPosToTickPos(end_ms);

    //Convert f0 list to pitch point
    var points = Enumerable.Zip(
        Enumerable.Range(0,f0_seq.Length),
        f0_seq,
        (i,f0)=>new Point{
            X=timeAxis.MsPosToTickPos(i*f0_timestep_ms + offset_ms),
            Y=MusicMath.FreqToTone(f0)
        }
    ).ToList();

    var tick_ep = 5;
    var notesInPhrase = uNotes
        .Where(note => 
            note.position >= offset_tick - tick_ep && 
            note.position <= end_tick + tick_ep
        ).ToList();
    
    //Reduce pitch point
    List<int> mustIncludeIndices = notesInPhrase
        .SelectMany(n => new[] { 
            n.position, 
            n.duration>160 ? n.end-80 : n.position+n.duration/2 })
        .Select(tick=>(int)((timeAxis.TickPosToMsPos(tick)-offset_ms)/f0_timestep_ms))
        .Where(msPos=> msPos>=0 && msPos<points.Count-1)
        .Prepend(0)
        .Append(points.Count-1)
        .ToList();
    //pairwise(mustIncludePointIndices) 
    points = mustIncludeIndices.Zip(mustIncludeIndices.Skip(1), 
            (a, b) => PitchUtil.simplifyShape(points.GetRange(a,b-a),0.1))
        .SelectMany(x=>x).Append(points[^1]).ToList();

    //determine where to distribute pitch point
    int idx = 0;
    //note_boundary[i] is the index of the first pitch point after the end of note i
    var note_boundaries = new int[notesInPhrase.Count + 1];
    note_boundaries[0] = 2;
    foreach(int i in Enumerable.Range(0,notesInPhrase.Count)) {
        var note = notesInPhrase[i];
        while(idx<points.Count 
            && points[idx].X<note.end){
            idx++;
        }
        note_boundaries[i + 1] = idx;
    }
    //if there is zero point in the note, adjusted_boundaries is the index of the last zero point
    //otherwise, it is the index of the pitch point with minimal y-distance to the note
    var adjusted_boundaries = new int[notesInPhrase.Count + 1];
    adjusted_boundaries[0] = 2;
    foreach(int i in Enumerable.Range(0,notesInPhrase.Count - 1)){
        var note = notesInPhrase[i];
        var notePitch = note.tone;
        //var zero_point = points.FindIndex(note_boundaries[i], note_boundaries[i + 1] - note_boundaries[i], p => p.Y == 0);
        var zero_point = Enumerable.Range(0,note_boundaries[i + 1] - note_boundaries[i])
            .Select(j=>note_boundaries[i+1]-1-j)
            .Where(j => (points[j].Y-notePitch) * (points[j-1].Y-notePitch) <= 0)
            .DefaultIfEmpty(-1)
            .First();
        if(zero_point != -1){
            adjusted_boundaries[i + 1] = zero_point + 1;
        }else{
            adjusted_boundaries[i + 1] = PitchUtil.LastIndexOfMin(points, p => Math.Abs(p.Y - notePitch), note_boundaries[i], note_boundaries[i + 1]) + 2;
        }
    }
    adjusted_boundaries[^1] = note_boundaries[^1];

    //distribute pitch point to each note
    foreach(int noteId in Enumerable.Range(0,notesInPhrase.Count)) {
        var note = notesInPhrase[noteId];
        var pointsInNote = points.GetRange(adjusted_boundaries[noteId]-2,adjusted_boundaries[noteId + 1]-(adjusted_boundaries[noteId]-2));
        //pairwise(pointsInNote)
        float PBS = (float)timeAxis.MsBetweenTickPos(note.position, pointsInNote[0].X);
        float[] PBW = new float[pointsInNote.Count-1];
        float[] PBY = new float[pointsInNote.Count-1];
        string[] PBM = new string[pointsInNote.Count-1];
        foreach(int pointId in Enumerable.Range(0,pointsInNote.Count-1)){
            var currentPoint = pointsInNote[pointId];
            var nextPoint = pointsInNote[pointId+1];
            PBW[pointId] = (float)timeAxis.MsBetweenTickPos(currentPoint.X, nextPoint.X);
            PBY[pointId] = (float)(nextPoint.Y - note.tone) * 10;
            PBM[pointId] = PitchUtil.ToUtauPBM(currentPoint.shape);
        }
            if(note.pluginNote!=null){
            note.pluginNote.SetPbs(PBS.ToString());
            note.pluginNote.SetPbw(string.Join(",",PBW));
            note.pluginNote.SetPby(string.Join(",",PBY));
            note.pluginNote.SetPbm(string.Join(",",PBM));
        }
    }
}

//write tmp ust file
plugin.Output();