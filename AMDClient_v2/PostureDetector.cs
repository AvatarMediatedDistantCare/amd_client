using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Kinect;

namespace AMDClient_v2
{
    class PostureType
    {
        public const int Standing = 0;
        public const int Seated = 1;
        public const int LyingHeadLeft = 2;
        public const int LyingHeadRight = 3;
    }
    class PostureDetector
    {
        public static int Detect(Body body)
        {
            float threshold_y1 = -0.7f;
            float spinebase_y = body.Joints[JointType.SpineBase].Position.Y;

            if (spinebase_y > threshold_y1)
            {
                return PostureType.Standing;
            }
            float threshold_y2 = 0.2f;
            float head_y = body.Joints[JointType.Head].Position.Y;
            if (head_y - spinebase_y <= threshold_y2)
            {
                if (body.Joints[JointType.Head].Position.X > body.Joints[JointType.SpineBase].Position.X)
                {
                    return PostureType.LyingHeadLeft;
                }
                return PostureType.LyingHeadRight;
            }
            return PostureType.Seated;
        }
    }
}
