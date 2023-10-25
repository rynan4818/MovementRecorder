using System.Collections.Generic;

namespace MovementRecorder.Models
{
    public class MovementJson
    {
        public int objectCount { get; set; }
        public int recordCount { get; set; }
        public string levelID { get; set; }
        public string songName { get; set; }
        public string serializedName { get; set; }
        public string difficulty { get; set; }
        public List<Setting> Settings { get; set; }
        public int recordFrameRate { get; set; }
        public List<string> objectNames { get; set; }
        public List<Scale> objectScales { get; set; }
    }
    public class Setting
    {
        public virtual string name { get; set; }
        public virtual string type { get; set; }
        public virtual List<string> topObjectStrings { get; set; }
        public virtual string rescaleString { get; set; }
        public virtual List<string> searchStirngs { get; set; }
        public virtual List<string> exclusionStrings { get; set; }
    }
    public class Scale
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
    }
    public class ResearchJson
    {
        public List<string> recordObjectNames { get; set; }
        public List<string> motionLocalEnabled { get; set; }
        public List<string> motionWorldEnabled { get; set; }
        public List<MotionScales> otherOneScales { get; set; }
    }
    public class MotionScales
    {
        public string objectName { get; set; }
        public string scale { get; set; }
    }
}
