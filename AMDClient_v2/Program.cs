using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WebSocket4Net;
using Microsoft.Kinect;
using Microsoft.Kinect.Face;

using System.IO;
using System.Threading;

using AMDClient_v2.Serializer;


namespace AMDClient_v2
{
    class Program
    {
        static WebSocket socket;

        static KinectSensor sensor;

        static int _bodyCount;

        static CoordinateMapper _coordinateMapper;
        static BodyFrameReader _bodyFrameReader;
        static Body[] _bodies;

        static FaceFrameSource[] _faceFrameSources;
        static FaceFrameReader[] _faceFrameReaders;
        static FaceFrameResult[] _faceFrameResults;

        static private String role;

        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to Avatar Mediated Distant-Care System.");
            Console.WriteLine("Choose your role.");
            Console.WriteLine("0: Elderly Side");
            Console.WriteLine("1: Caregiver Side");
            Console.Write("Enter 0 or 1: ");
            int role_num = int.Parse(Console.ReadLine());

            switch (role_num)
            {
                case 0:
                    // elderly
                    role = "actor";
                    break;
                case 1:
                    // caregiver
                    role = "observer";
                    break;
            }

            if (role != null)
            {
                Console.WriteLine("Welcome " + role);
            }
            socket = new WebSocket("ws://" + Properties.Resource.AMDServerHost + "/ws_kinect");

            socket.Opened += (s, e) =>
            {
                Console.WriteLine("{0}: Server connected.", DateTime.Now.ToString());
                socket.Send("{\"role\":\"" + role + "\"}");
            };

            socket.Open();

            initializeKinectV2();

            while (Console.ReadKey().Key != ConsoleKey.Escape)
            {
                System.Threading.Thread.Sleep(2000);
            }
        }

        private static void initializeKinectV2()
        {
            sensor = KinectSensor.GetDefault();

            _bodyCount = sensor.BodyFrameSource.BodyCount;

            _coordinateMapper = sensor.CoordinateMapper;

            // enable body frame
            _bodyFrameReader = sensor.BodyFrameSource.OpenReader();
            _bodyFrameReader.FrameArrived += _bodyFrameReader_FrameArrived;

            // allocate buffer to store bodies
            _bodies = new Body[_bodyCount];

            // specify the required face frame results
            FaceFrameFeatures faceFrameFeatures =
                FaceFrameFeatures.RotationOrientation
                | FaceFrameFeatures.MouthOpen
                | FaceFrameFeatures.MouthMoved
                | FaceFrameFeatures.LeftEyeClosed
                | FaceFrameFeatures.RightEyeClosed;

            _faceFrameSources = new FaceFrameSource[_bodyCount];
            _faceFrameReaders = new FaceFrameReader[_bodyCount];
            for (int i = 0; i < _bodyCount; i++)
            {
                _faceFrameSources[i] = new FaceFrameSource(sensor, 0, faceFrameFeatures);
                _faceFrameReaders[i] = _faceFrameSources[i].OpenReader();
                _faceFrameReaders[i].FrameArrived += _faceFrameReader_FrameArrived;
            }

            // allocate buffer to store face frame results
            _faceFrameResults = new FaceFrameResult[_bodyCount];

            sensor.Open();
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        static void _bodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (_bodies == null)
                    {
                        _bodies = new Body[bodyFrame.BodyCount];
                    }
                    bodyFrame.GetAndRefreshBodyData(_bodies);
                    dataReceived = true;
                }
            }

            if (!dataReceived) return;


            // update the face frame source to track this body
            for (int i = 0; i < _bodyCount; i++)
            {
                if (!_faceFrameSources[i].IsTrackingIdValid || _faceFrameSources[i].TrackingId == 0)
                {
                    if (!_bodies[i].IsTracked)
                    {
                        continue;
                    }
                    _faceFrameSources[i].TrackingId = _bodies[i].TrackingId;
                }
            }

            var users = _bodies.Where(s => s.IsTracked).ToList();

            if (users.Count == 0) return;

            string json = users.Serialize(_coordinateMapper, _faceFrameResults);

            if (socket.State == WebSocketState.Open)
            {
                socket.Send(json);
            }
        }

        static void _faceFrameReader_FrameArrived(object sender, FaceFrameArrivedEventArgs e)
        {
            using (FaceFrame faceFrame = e.FrameReference.AcquireFrame())
            {
                // get the index of the face source from the face source array
                int index = GetFaceSourceIndex(faceFrame.FaceFrameSource);

                if (faceFrame == null || !faceFrame.IsTrackingIdValid)
                {
                    _faceFrameResults[index] = null;
                    return;
                }
                

                // store this face frame result
                _faceFrameResults[index] = faceFrame.FaceFrameResult;
            }
        }

        /// <summary>
        /// Returns the index of the face frame source
        /// </summary>
        /// <param name="faceFrameSource">the face frame source</param>
        /// <returns>the index of the face source in the face source array</returns>
        private static int GetFaceSourceIndex(FaceFrameSource faceFrameSource)
        {
            int index = -1;

            for (int i = 0; i < _bodyCount; i++)
            {
                if (_faceFrameSources[i] == faceFrameSource)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }
    }
}
