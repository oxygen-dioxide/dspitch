using System.Text;
using System.Text.Json;

namespace DsPitch{
    [Serializable]
    public class RawDiffSingerScript {
        
        public string f0_seq { get; set; }
        public string f0_timestep { get; set; }
        public double offset { get; set; }

        public RawDiffSingerScript() { 
            offset = 0;
            f0_seq = null;
            f0_timestep = "0.005";
        }

        public static RawDiffSingerScript[] Parse(string json){
            var document = JsonDocument.Parse(json);
            if(document.RootElement.ValueKind == JsonValueKind.Array)
                return JsonSerializer.Deserialize<RawDiffSingerScript[]>(json);
            else if(document.RootElement.ValueKind == JsonValueKind.Object)
                return new RawDiffSingerScript[]{JsonSerializer.Deserialize<RawDiffSingerScript>(json)};
            else
                throw new System.Exception("Invalid json format");
        }

        public static RawDiffSingerScript[] Load(string filename){
            return Parse(System.IO.File.ReadAllText(filename,Encoding.UTF8));
        }
    }

    public class DiffSingerScript{
        public double[]? f0_seq;
        public double f0_timestep;
        public double offset = 0;

        public static DiffSingerScript FromRaw(RawDiffSingerScript raw){
            return new DiffSingerScript {
                offset = raw.offset,
                f0_seq = raw.f0_seq?.Split(' ').Select(double.Parse).ToArray() ?? null,
                f0_timestep = double.Parse(raw.f0_timestep)
            };
        }

        public static DiffSingerScript[] FromRaw(RawDiffSingerScript[] raw){
            return raw.Select(FromRaw).ToArray();
        }
        public static DiffSingerScript[] Load(string filename){
            return FromRaw(RawDiffSingerScript.Load(filename));
        }
    }
}