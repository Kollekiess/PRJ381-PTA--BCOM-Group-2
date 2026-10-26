using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace BCSignVR.Tests
{
    public sealed class ProfileNetworkIsolationTests
    {
        GameObject owner;
        BCSignVRApp app;
        HttpListener server;
        IEnumerator pending;
        string directory;
        LocalSessionStore firstHistory, secondHistory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "signzzz-account-test-" + Guid.NewGuid().ToString("N"));
            firstHistory = new LocalSessionStore(Path.Combine(directory, "first.json"));
            secondHistory = new LocalSessionStore(Path.Combine(directory, "second.json"));
            var portProbe = new TcpListener(IPAddress.Loopback, 0);
            portProbe.Start();
            var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
            portProbe.Stop();
            var address = "http://127.0.0.1:" + port;
            server = new HttpListener();
            server.Prefixes.Add(address + "/");
            server.Start();
            owner = new GameObject("Inactive account test");
            owner.SetActive(false); // Do not initialize the classroom or touch real learner files.
            app = owner.AddComponent<BCSignVRApp>();
            Set("backendBaseUrl", address);
            Set("activeProfile", new LearnerProfile { id = "first", displayName = "First" });
            Set("history", firstHistory);
            Set("profileRevision", 1);
            Set("token", "first-token");
            Set("apiConnected", true);
        }

        [TearDown]
        public void TearDown()
        {
            (pending as IDisposable)?.Dispose();
            server?.Close();
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator LateLoginCannotReplaceTheNewLearnersSession()
        {
            var incoming = server.GetContextAsync();
            var operation = Begin("ConnectBackend");
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!incoming.IsCompleted) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
            SwitchLearner();
            Reply(incoming.Result, "{\"token\":\"first-response-token\"}");
            while (!operation.isDone) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
            Assert.IsFalse(pending.MoveNext(), "The old login must stop before starting a sync for the new learner.");
            Assert.AreEqual("second-token", Get<string>("token"));
            Assert.IsTrue(Get<bool>("apiConnected"));
        }

        [UnityTest]
        public IEnumerator LateProgressCannotReplaceTheNewLearnersTotals()
        {
            var incoming = server.GetContextAsync();
            var operation = Begin("RefreshRemoteProgress");
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!incoming.IsCompleted) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
            SwitchLearner();
            Set("remoteCompleted", 2);
            Set("remoteAverage", .4f);
            Reply(incoming.Result, "{\"summary\":{\"completed\":99,\"averageAccuracy\":1.0}}");
            while (!operation.isDone) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
            Assert.IsFalse(pending.MoveNext());
            Assert.AreEqual(2, Get<int>("remoteCompleted"));
            Assert.AreEqual(.4f, Get<float>("remoteAverage"));
        }

        [UnityTest]
        public IEnumerator LateUploadCannotMarkAnotherLearnersRecordSynced()
        {
            var record = new LocalSessionRecord { id = "same-id", lessonId = "prototype-open",
                accuracy = .8f, durationSeconds = 2f, completed = true, inputSource = "openxr" };
            firstHistory.Add(record);
            secondHistory.Add(record);
            var incoming = server.GetContextAsync();
            var operation = Begin("SyncPending");
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!incoming.IsCompleted) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
            Assert.AreEqual("Bearer first-token", incoming.Result.Request.Headers["Authorization"]);
            SwitchLearner();
            Set("syncing", true); // The new learner may already have their own upload running.
            Reply(incoming.Result, "{}");
            while (!operation.isDone) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
            Assert.IsFalse(pending.MoveNext());
            Assert.AreEqual(1, secondHistory.PendingCount);
            Assert.AreEqual(1, new LocalSessionStore(secondHistory.FilePath).PendingCount);
            Assert.IsTrue(Get<bool>("syncing"), "An old request must not clear the new learner's upload state.");
        }

        UnityWebRequestAsyncOperation Begin(string name)
        {
            pending = (IEnumerator)typeof(BCSignVRApp).GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, null);
            Assert.IsTrue(pending.MoveNext());
            return (UnityWebRequestAsyncOperation)pending.Current;
        }

        void SwitchLearner()
        {
            Set("activeProfile", new LearnerProfile { id = "second", displayName = "Second" });
            Set("history", secondHistory);
            Set("profileRevision", 2);
            Set("token", "second-token");
            Set("apiConnected", true);
        }

        static void Reply(HttpListenerContext context, string json)
        {
            using (var body = new StreamReader(context.Request.InputStream)) body.ReadToEnd();
            var bytes = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        void Set(string name, object value) => typeof(BCSignVRApp)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(app, value);
        T Get<T>(string name) => (T)typeof(BCSignVRApp)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
    }
}
