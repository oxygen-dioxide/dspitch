using utauPlugin;

namespace DsPitch
{
    public class UNote
    {
        public int position;
        public int duration;
        public int tone;
        public string lyric = "";
        public Note? pluginNote;

        public int end => position + duration;

        //if the plugin note has tempo change, return its UTempo
        //otherwise return null
        public UTempo? getTempo(){
            if(pluginNote!=null && pluginNote.HasTempo()){
                return new UTempo(position, pluginNote.GetTempo());
            }
            return null;
        }
    }

    public class Phrase{
        public List<UNote> notes;
        public int headPos;//the start position of the rest note before the phrase
        public int End => notes[^1].end;
    }
}