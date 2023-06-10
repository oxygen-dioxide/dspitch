using utauPlugin;

namespace DsPitch
{
    public class UtauUtil
    {
        public static bool isRest(Note note){
            var lyric = note.GetLyric();
            return lyric == "R" || lyric == "r";
        }

        //get a list of positions for each note, including rest notes
        public static List<UNote> PluginNotesToUNotes(UtauPlugin plugin){
            var uNotes = new List<UNote>();
            var currentPosition = 0;
            foreach(var note in plugin.note){
                //positions.Add(currentPosition);
                var duration = note.GetLength();
                if(!(isRest(note))){
                    uNotes.Add(new UNote{
                        position = currentPosition,
                        duration = duration,
                        tone = note.GetNoteNum(),
                        lyric = note.GetLyric(),
                        pluginNote = note
                    });
                }
                currentPosition += duration;
            }
            return uNotes;
        }

        public static List<Phrase> UNotesToPhrases(List<UNote> unotes){
            var phrases = new List<Phrase>();
            var currentPhrase = new Phrase{
                notes = new List<UNote>(),
                headPos = 0
            };
            var currentPosition = 0;
            foreach(var note in unotes){
                if(note.position > currentPosition){
                    //end the current phrase and staart a new one
                    if(currentPhrase.notes.Count>0){
                        phrases.Add(currentPhrase);
                        currentPhrase = new Phrase{
                            notes = new List<UNote>(),
                            headPos = currentPosition
                        };
                    }
                }
                currentPhrase.notes.Add(note);
                currentPosition = note.end;
            }
            if(currentPhrase.notes.Count>0){
                phrases.Add(currentPhrase);
            }
            return phrases;
        }

        public static List<UTempo> GetTempoList(UtauPlugin plugin, List<UNote> uNotes){
            var tempos = new List<UTempo>{
                new UTempo(0, plugin.Tempo)
            };
            tempos.AddRange(uNotes
                .Select(n=>n.getTempo())
                .Where(t=>t!=null));
            return tempos;
        }
    }
}