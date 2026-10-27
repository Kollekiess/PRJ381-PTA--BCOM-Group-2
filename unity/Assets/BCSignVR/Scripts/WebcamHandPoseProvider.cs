using System;
using System.IO;
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
        readonly int port;
        readonly bool useTcp;
        UdpClient udpClient;
        readonly Thread receiver;
        TcpClient activeTcpClient;
        Packet latest;
        DateTime latestAt;
        bool transportConnected;
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

        // Quest reaches the Mac bridge through adb reverse, which forwards TCP.
        // Desktop builds retain the existing UDP loopback path for the simulator.
        public bool EndpointReady
        {
            get
            {
                lock (sync)
                    return useTcp ? transportConnected : latest != null && (DateTime.UtcNow - latestAt).TotalSeconds <= 1.0;
            }
        }

        public WebcamHandPoseProvider(int port = 5055)
        {
            this.port = port;
            useTcp = Application.platform == RuntimePlatform.Android ||
                Environment.GetEnvironmentVariable("BCSIGNVR_WEBCAM_TRANSPORT") == "tcp";
            try
            {
                if (!useTcp)
                {
                    udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                    udpClient.Client.ReceiveTimeout = 500;
                    receiver = new Thread(ReceiveUdpLoop) { IsBackground = true, Name = "BCSignVR webcam UDP receiver" };
                }
                else
                {
                    receiver = new Thread(ReceiveTcpLoop) { IsBackground = true, Name = "BCSignVR webcam TCP receiver" };
                }
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
            // Keep the live camera card visible during tracking loss. A blank
            // card should mean the camera is unavailable, not simply that the
            // learner has moved their hand out of frame.
            if (packet != null && !packet.tracked)
                PreviewJpeg = "";
            else if (packet != null && !string.IsNullOrEmpty(packet.previewJpeg))
                PreviewJpeg = packet.previewJpeg;
            if (packet == null || packet.version != 1 || !packet.tracked || packet.landmarks == null || packet.landmarks.Length != 21 ||
                (DateTime.UtcNow - received).TotalSeconds > .45)
                return false;
            foreach (var point in packet.landmarks)
                if (point == null || !Finite(point.x) || !Finite(point.y) || !Finite(point.z)) return false;
            MapMediaPipe(packet.landmarks, destination);
            DetectedHand = string.IsNullOrWhiteSpace(packet.handedness) ? "Right" : packet.handedness;
            RecognizedLabel = string.IsNullOrEmpty(packet.recognized) ? "UNKNOWN" : packet.recognized;
            RecognitionConfidence = Finite(packet.confidence) ? Mathf.Clamp01(packet.confidence) : 0;
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

        void StorePacket(string payload)
        {
            try
            {
                var packet = JsonUtility.FromJson<Packet>(payload);
                if (packet == null) return;
                lock (sync) { latest = packet; latestAt = DateTime.UtcNow; }
            }
            catch (Exception error) { Debug.LogWarning($"Bad webcam landmark packet: {error.Message}"); }
        }

        void ReceiveUdpLoop()
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);
            while (running && udpClient != null)
            {
                try
                {
                    var bytes = udpClient.Receive(ref endpoint);
                    StorePacket(Encoding.UTF8.GetString(bytes));
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { break; }
            }
        }

        void ReceiveTcpLoop()
        {
            while (running)
            {
                TcpClient connection = null;
                try
                {
                    connection = new TcpClient { NoDelay = true };
                    connection.Connect(IPAddress.Loopback, port);
                    lock (sync)
                    {
                        activeTcpClient = connection;
                        transportConnected = true;
                    }
                    using var reader = new StreamReader(connection.GetStream(), Encoding.UTF8);
                    while (running)
                    {
                        var line = reader.ReadLine();
                        if (line == null) break;
                        StorePacket(line);
                    }
                }
                catch (SocketException) { }
                catch (IOException) { }
                catch (ObjectDisposedException) { break; }
                finally
                {
                    lock (sync)
                    {
                        transportConnected = false;
                        if (ReferenceEquals(activeTcpClient, connection)) activeTcpClient = null;
                    }
                    connection?.Close();
                }
                if (running) Thread.Sleep(250);
            }
        }

        public void Dispose()
        {
            running = false;
            udpClient?.Close();
            lock (sync) { activeTcpClient?.Close(); }
            receiver?.Join(700);
        }
    }
}
