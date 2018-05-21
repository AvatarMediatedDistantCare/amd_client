using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;

using Microsoft.Kinect;
using Microsoft.Kinect.Face;

namespace AMDClient_v2.Serializer
{
    public static class BodySerializer
    {
        [DataContract]
        class JSONBodyCollection
        {
            [DataMember(Name = "bodies")]
            public List<JSONBody> Bodies { get; set; }
        }

        [DataContract]
        class JSONBody
        {
            [DataMember(Name = "id")]
            public string ID { get; set; }

            //[DataMember(Name = "audio_f")]
            //public bool Audio_flag { get; set; }

            [DataMember(Name = "posture")]
            public int Posture { get; set; }

            [DataMember(Name = "joints")]
            public List<JSONJoint> Joints { get; set; }

            [DataMember(Name = "face")]
            public JSONFace Face { get; set; }
        }

        [DataContract]
        class JSONJoint
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "map_x")]
            public double MapX { get; set; }

            [DataMember(Name = "map_y")]
            public double MapY { get; set; }

            [DataMember(Name = "map_z")]
            public double MapZ { get; set; }

            [DataMember(Name = "x")]
            public double X { get; set; }

            [DataMember(Name = "y")]
            public double Y { get; set; }

            [DataMember(Name = "z")]
            public double Z { get; set; }

            [DataMember(Name = "quaternion_x")]
            public double Quaternion_X { get; set; }

            [DataMember(Name = "quaternion_y")]
            public double Quaternion_Y { get; set; }

            [DataMember(Name = "quaternion_z")]
            public double Quaternion_Z { get; set; }

            [DataMember(Name = "quaternion_w")]
            public double Quaternion_W { get; set; }

            [DataMember(Name = "is_tracked")]
            public Boolean IsTracked { get; set; }
        }

        [DataContract]
        class JSONFace
        {
            [DataMember(Name = "quaternion_x")]
            public double Quaternion_X { get; set; }

            [DataMember(Name = "quaternion_y")]
            public double Quaternion_Y { get; set; }

            [DataMember(Name = "quaternion_z")]
            public double Quaternion_Z { get; set; }

            [DataMember(Name = "quaternion_w")]
            public double Quaternion_W { get; set; }

            [DataMember(Name = "mouth_opened")]
            public bool MouthOpened { get; set; }

            [DataMember(Name = "mouth_moved")]
            public bool MouthMoved { get; set; }

            [DataMember(Name = "left_eye_closed")]
            public bool LeftEyeClosed { get; set; }

            [DataMember(Name = "right_eye_closed")]
            public bool RightEyeClosed { get; set; }
        }

        /// <summary>
        /// Serializes an array of Kinect skeletons into an array of JSON skeletons.
        /// </summary>
        /// <param name="bodies">The Kinect bodies.</param>
        /// <param name="mapper">The coordinate mapper.</param>
        /// <param name="faceFrameResults">The kinect faces.</param>
        /// <returns>A JSON representation of the skeletons.</returns>
        public static string Serialize(this List<Body> bodies, CoordinateMapper mapper, FaceFrameResult[] faceFrameResults)
        {
            JSONBodyCollection jsonBodies = new JSONBodyCollection { Bodies = new List<JSONBody>() };

            foreach (Body body in bodies)
            {
                JSONBody jsonBody = new JSONBody
                {
                    ID = body.TrackingId.ToString(),
                    Joints = new List<JSONJoint>()
                };
                
                foreach (KeyValuePair<JointType, Joint> jointpair in body.Joints)
                {
                    Joint joint = jointpair.Value;
                    
                    DepthSpacePoint depthPoint = mapper.MapCameraPointToDepthSpace(joint.Position);

                    jsonBody.Joints.Add(new JSONJoint
                    {
                        Name = joint.JointType.ToString().ToLower(),
                        MapX = depthPoint.X,
                        MapY = depthPoint.Y,
                        MapZ = joint.Position.Z,
                        X = body.Joints[joint.JointType].Position.X,
                        Y = body.Joints[joint.JointType].Position.Y,
                        Z = body.Joints[joint.JointType].Position.Z,

                        // absolute
                        Quaternion_W = body.JointOrientations[joint.JointType].Orientation.W,
                        Quaternion_X = body.JointOrientations[joint.JointType].Orientation.X,
                        Quaternion_Y = body.JointOrientations[joint.JointType].Orientation.Y,
                        Quaternion_Z = body.JointOrientations[joint.JointType].Orientation.Z,

                        IsTracked = (body.Joints[joint.JointType].TrackingState == TrackingState.Tracked)
                    });
                }

                // faceとbodyの関連付け
                FaceFrameResult associatedFace = null;
                foreach (var f in faceFrameResults)
                {
                    if (f == null) continue;
                    if (f.TrackingId == body.TrackingId)
                    {
                        associatedFace = f;
                        break;
                    }
                }
                if (associatedFace != null)
                {
                    jsonBody.Face = new JSONFace
                    {
                        Quaternion_W = associatedFace.FaceRotationQuaternion.W,
                        Quaternion_X = associatedFace.FaceRotationQuaternion.X,
                        Quaternion_Y = associatedFace.FaceRotationQuaternion.Y,
                        Quaternion_Z = associatedFace.FaceRotationQuaternion.Z,

                        MouthOpened = (associatedFace.FaceProperties[FaceProperty.MouthOpen] == DetectionResult.Maybe || associatedFace.FaceProperties[FaceProperty.MouthOpen] == DetectionResult.Yes),
                        MouthMoved  = (associatedFace.FaceProperties[FaceProperty.MouthMoved] == DetectionResult.Maybe || associatedFace.FaceProperties[FaceProperty.MouthMoved] == DetectionResult.Yes),
                        LeftEyeClosed  = (associatedFace.FaceProperties[FaceProperty.LeftEyeClosed] == DetectionResult.Maybe || associatedFace.FaceProperties[FaceProperty.LeftEyeClosed] == DetectionResult.Yes),
                        RightEyeClosed = (associatedFace.FaceProperties[FaceProperty.RightEyeClosed] == DetectionResult.Maybe || associatedFace.FaceProperties[FaceProperty.RightEyeClosed] == DetectionResult.Yes)
                    };
                }

                // 立っている, 座っている, 寝ている の判定
                int posture = PostureDetector.Detect(body);
                jsonBody.Posture = posture;

                jsonBodies.Bodies.Add(jsonBody);
            }

            return Serialize(jsonBodies);
        }

        private static string Serialize(object obj)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());

            using (MemoryStream ms = new MemoryStream())
            {
                serializer.WriteObject(ms, obj);

                return Encoding.Default.GetString(ms.ToArray());
            }
        }
    }
}
