using System.Collections;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace BCSignVR.Tests
{
    public sealed class WebcamHandPoseProviderTests
    {
        [UnityTest]
        public IEnumerator ReceivesReviewedRecognitionAndMapsTheHand()
        {
            const int port = 15055;
            using var provider = new BCSignVR.WebcamHandPoseProvider(port);
            using var sender = new UdpClient();
            var points = new StringBuilder("[");
            for (var index = 0; index < 21; index++)
            {
                if (index > 0) points.Append(',');
                points.AppendFormat(CultureInfo.InvariantCulture,
                    "{{\"x\":{0},\"y\":{1},\"z\":{2}}}",
                    .45f + index * .002f, .35f + index * .003f, -index * .01f);
            }
            points.Append(']');
            var timestamp = (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0).ToString(CultureInfo.InvariantCulture);
            var json = $"{{\"version\":1,\"timestamp\":{timestamp},\"tracked\":true,\"handedness\":\"Right\",\"recognized\":\"REVIEWED_SASL_HELLO\",\"confidence\":0.91,\"landmarks\":{points}}}";
            var payload = Encoding.UTF8.GetBytes(json);
            sender.Send(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, port));

            var frame = new BCSignVR.HandPoseFrame();
            var received = false;
            for (var attempt = 0; attempt < 20 && !received; attempt++)
            {
                yield return null;
                received = provider.TryGetRightHand(frame);
            }

            Assert.That(received, Is.True);
            Assert.That(frame.tracked, Is.True);
            Assert.That(provider.RecognizedLabel, Is.EqualTo("REVIEWED_SASL_HELLO"));
            Assert.That(provider.RecognitionConfidence, Is.EqualTo(.91f).Within(.001f));
            Assert.That(frame.joints[0], Is.Not.EqualTo(UnityEngine.Vector3.zero));
        }
    }
}
