using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DsPitch {
    public class UTempo {
        public int position;
        public double bpm;

        public UTempo() { }
        public UTempo(int position, double bpm) {
            this.position = position;
            this.bpm = bpm;
        }
        public override string ToString() => $"{bpm}@{position}";
    }

    public class TimeAxis {
        public const int resolution = 480;
        class TimeSigSegment {
            public int barPos;
            public int barEnd = int.MaxValue;
            public int tickPos;
            public int tickEnd = int.MaxValue;
        }

        class TempoSegment {
            public int tickPos;
            public int tickEnd = int.MaxValue;

            public double bpm;

            public double msPos;
            public double msEnd = double.MaxValue;
            public double msPerTick;
            public double ticksPerMs;

            public int Ticks => tickEnd - tickPos;
        }
        readonly List<TempoSegment> tempoSegments = new List<TempoSegment>();

        public long Timestamp { get; private set; }

        public void BuildSegments(List<UTempo> tempos) {
            Timestamp = DateTime.Now.ToFileTimeUtc();

            tempoSegments.Clear();
            tempoSegments.Add(new TempoSegment {
                tickPos = 0,
                bpm = 120,
            });
            for (var i = 0; i < tempos.Count; ++i) {
                var tempo = tempos[i];
                if (i == 0) {
                    Debug.Assert(tempo.position == 0);
                }
                var index = tempoSegments.FindIndex(seg => seg.tickPos >= tempo.position);
                if (index < 0) {
                    tempoSegments.Add(new TempoSegment {
                        tickPos = tempo.position,
                        bpm = tempo.bpm,
                    });
                } else if (tempoSegments[index].tickPos == tempo.position) {
                    tempoSegments[index].bpm = tempo.bpm;
                } else {
                    tempoSegments.Insert(index, new TempoSegment {
                        tickPos = tempo.position,
                        bpm = tempo.bpm,
                    });
                }
            }
            for (var i = 0; i < tempoSegments.Count - 1; ++i) {
                if (tempoSegments[i + 1].bpm == 0) {
                    tempoSegments[i + 1].bpm = tempoSegments[i].bpm;
                }
                tempoSegments[i].tickEnd = tempoSegments[i + 1].tickPos;
            }
            for (var i = 0; i < tempoSegments.Count; ++i) {
                tempoSegments[i].msPerTick = 60.0 * 1000.0 / (tempoSegments[i].bpm * resolution);
                tempoSegments[i].ticksPerMs = tempoSegments[i].bpm * resolution / (60.0 * 1000.0);
                if (i > 0) {
                    tempoSegments[i].msPos = tempoSegments[i - 1].msPos + tempoSegments[i - 1].Ticks * tempoSegments[i - 1].msPerTick;
                    tempoSegments[i - 1].msEnd = tempoSegments[i].msPos;
                }
            }
        }

        public double GetBpmAtTick(int tick) {
            var segment = tempoSegments.First(seg => seg.tickPos == tick || seg.tickEnd > tick); // TODO: optimize
            return segment.bpm;
        }

        public double TickPosToMsPos(double tick) {
            var segment = tempoSegments.First(seg => seg.tickPos == tick || seg.tickEnd > tick); // TODO: optimize
            return segment.msPos + segment.msPerTick * (tick - segment.tickPos);
        }

        public int MsPosToTickPos(double ms) {
            var segment = tempoSegments.First(seg => seg.msPos == ms || seg.msEnd > ms); // TODO: optimize
            double tickPos = segment.tickPos + (ms - segment.msPos) * segment.ticksPerMs;
            return (int)Math.Round(tickPos);
        }

        public int TicksBetweenMsPos(double msPos, double msEnd) {
            return MsPosToTickPos(msEnd) - MsPosToTickPos(msPos);
        }

        public double MsBetweenTickPos(double tickPos, double tickEnd) {
            return TickPosToMsPos(tickEnd) - TickPosToMsPos(tickPos);
        }

        public UTempo[] TemposBetweenTicks(int start, int end) {
            var list = tempoSegments
                .Where(tempo => start < tempo.tickEnd && tempo.tickPos < end)
                .Select(tempo => new UTempo { position = tempo.tickPos, bpm = tempo.bpm })
                .ToArray();
            return list;
        }

        public TimeAxis Clone() {
            var clone = new TimeAxis();
            // Shallow copy segments since they are unmodified after built.
            clone.tempoSegments.AddRange(tempoSegments);
            return clone;
        }
    }
}