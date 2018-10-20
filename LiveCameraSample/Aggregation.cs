using System.Linq;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Common.Contract;
using System;
using System.Collections.Generic;

namespace LiveCameraSample
{
    internal class Aggregation
    {
        public static Tuple<string, float> GetDominantEmotion(EmotionScores scores)
        {
            return scores.ToRankedList().Select(kv => new Tuple<string, float>(kv.Key, kv.Value)).First();
        }

        public static string SummarizeEmotion(EmotionScores scores)
        {
            var bestEmotion = Aggregation.GetDominantEmotion(scores);
            return string.Format("{0}: {1:N1}", bestEmotion.Item1, bestEmotion.Item2);
        }

        public static string SummarizeFaceAttributes(FaceAttributes attr)
        {
            List<string> attrs = new List<string>();
            if (attr.Gender != null) attrs.Add(attr.Gender);
            if (attr.Age > 0) attrs.Add(attr.Age.ToString());
            if (attr.HeadPose != null)
            {
                // Simple rule to estimate whether person is facing camera. 
                bool facing = Math.Abs(attr.HeadPose.Yaw) < 25;
                attrs.Add(facing ? "facing camera" : "not facing camera");
            }
            return string.Join(", ", attrs);
        }
    }
}
