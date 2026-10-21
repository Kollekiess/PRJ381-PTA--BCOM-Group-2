using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace BCSignVR
{
    public sealed class WebcamHandPoseProvider : IHandPoseProvider, IDisposable
    {
        [Serializable] sealed class Landmark { public float x; public float y; public float z; }
        [Serializable] sealed class Packet
        {
            public int version;
            public double timestamp;
            public bool tracked;
            public string handedness;
            public string recognized;
            public float confidence;
            public string previewJpeg;
            public Landmark[] landmarks;
        }

        readonly object sync = new();
        readonly UdpClient client;
        readonly Thread receiver;
        Packet latest;
        DateTime latestAt;
        bool running = true;

        public string DisplayName => "Mac webcam hands";
        public string DetectedHand { get; private set; } = "Right";
        public string RecognizedLabel { get; private set; } = "COLLECTING_MOVEMENT";
        public float RecognitionConfidence { get; private set; }
        public string PreviewJpeg { get; private set; } = "";
        public bool IsReceiving
        {
            get
            {
                lock (sync) return latest != null && (DateTime.UtcNow - latestAt).TotalSeconds <= 1.0;
            }
        }

        public WebcamHandPoseProvider(int port = 5055)
        {
            try
            {
                client = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                client.Client.ReceiveTimeout = 500;
                receiver = new Thread(ReceiveLoop) { IsBackground = true, Name = "BCSignVR webcam receiver" };
                receiver.Start();
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Webcam hand receiver unavailable: {error.Message}");
            }
        }

        public bool TryGetRightHand(HandPoseFrame destination)
        {
            destination.tracked = false;
            RecognizedLabel = "UNKNOWN";
            RecognitionConfidence = 0;
            Packet packet;
            DateTime received;
            lock (sync) { packet = latest; received = latestAt; }
            if (packet == null || packet.version != 1 || !packet.tracked || packet.landmarks == null || packet.landmarks.Length != 21 ||
                (DateTime.UtcNow - received).TotalSeconds > .45)
                return false;
            foreach (var point in packet.landmarks)
                if (point == null || !Finite(point.x) || !Finite(point.y) || !Finite(point.z)) return false;
            MapMediaPipe(packet.landmarks, destination);
            DetectedHand = string.IsNullOrWhiteSpace(packet.handedness) ? "Right" : packet.handedness;
            RecognizedLabel = string.IsNullOrEmpty(packet.recognized) ? "UNKNOWN" : packet.recognized;
            RecognitionConfidence = Finite(packet.confidence) ? Mathf.Clamp01(packet.confidence) : 0;
            if (!string.IsNullOrEmpty(packet.previewJpeg)) PreviewJpeg = packet.previewJpeg;
            destination.tracked = true;
            destination.source = DisplayName;
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void MapMediaPipe(Landmark[] source, HandPoseFrame frame)
        {
            Vector3 Point(int index)
            {
                var p = source[index];
                return new Vector3(-(p.x - .5f), .5f - p.y, -p.z);
            }

            frame.joints[0] = Point(0);
            frame.joints[1] = (Point(0) + Point(5) + Point(9) + Point(13) + Point(17)) / 5f;
            for (var i = 0; i < 4; i++) frame.joints[2 + i] = Point(1 + i);
            MapFinger(source, frame, 6, 5, Point);
            MapFinger(source, frame, 11, 9, Point);
            MapFinger(source, frame, 16, 13, Point);
            MapFinger(source, frame, 21, 17, Point);
            // MediaPipe is already camera-aligned. Keeping this basis makes the board
            // behave like a mirror instead of unexpectedly twisting the learner's hand.
            frame.wristRotation = Quaternion.identity;
        }

        static void MapFinger(Landmark[] source, HandPoseFrame frame, int targetStart, int sourceStart,
            Func<int, Vector3> point)
        {
            frame.joints[targetStart] = Vector3.Lerp(point(0), point(sourceStart), .55f);
            for (var i = 0; i < 4; i++) frame.joints[targetStart + 1 + i] = point(sourceStart + i);
        }

        void ReceiveLoop()
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);
            while (running && client != null)
            {
                try
                {
                    var bytes = client.Receive(ref endpoint);
                    var packet = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(bytes));
                    lock (sync) { latest = packet; latestAt = DateTime.UtcNow; }
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { break; }
                catch (Exception error) { Debug.LogWarning($"Bad webcam landmark packet: {error.Message}"); }
            }
        }

        public void Dispose()
        {
            running = false;
            client?.Close();
            receiver?.Join(700);
        }
    }
}
