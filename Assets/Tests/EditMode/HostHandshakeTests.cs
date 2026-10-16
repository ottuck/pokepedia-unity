using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pokepedia.App.Host;

namespace Pokepedia.Tests
{
    public class HostHandshakeTests
    {
        sealed class FakeBridge : IHostBridge
        {
            public bool IsHostMode { get; set; }
            public readonly List<string> Posted = new();
            public event Action<InitMessage> InitReceived;
            public void Post(string json) => Posted.Add(json);
            public void Deliver(InitMessage m) => InitReceived?.Invoke(m);
        }

        static InitMessage Init(int v = 1) => new() { type = "init", v = v, locale = "ko", owned = new[] { new OwnedSticker { id = 25 } } };

        [Test]
        public void NoHostParam_GoesStraightToDemo_WithoutPosting()
        {
            var b = new FakeBridge { IsHostMode = false };
            var h = new HostHandshake(b);
            h.Begin(0);
            Assert.AreEqual(LaunchState.Demo, h.State);
            Assert.IsEmpty(b.Posted);
        }

        [Test]
        public void HostMode_PostsReady_ThenConnectsOnInit()
        {
            var b = new FakeBridge { IsHostMode = true };
            var h = new HostHandshake(b);
            h.Begin(0);
            Assert.AreEqual(LaunchState.WaitingInit, h.State);
            Assert.AreEqual(new[] { HostProtocol.ReadyJson }, b.Posted);

            b.Deliver(Init());
            Assert.AreEqual(LaunchState.Connected, h.State);
            Assert.AreEqual(25, h.Init.owned[0].id);
        }

        [Test]
        public void NoInitWithinTimeout_TimesOut_AndDoesNotFallBackToDemo()
        {
            var b = new FakeBridge { IsHostMode = true };
            var h = new HostHandshake(b);
            h.Begin(10);
            h.Tick(14.9);
            Assert.AreEqual(LaunchState.WaitingInit, h.State);
            h.Tick(15.0);
            Assert.AreEqual(LaunchState.TimedOut, h.State);
        }

        [Test]
        public void Retry_ResendsReady_AndRestartsTimer()
        {
            var b = new FakeBridge { IsHostMode = true };
            var h = new HostHandshake(b);
            h.Begin(0);
            h.Tick(5);
            h.Retry(20);
            Assert.AreEqual(2, b.Posted.Count);
            h.Tick(24);
            Assert.AreEqual(LaunchState.WaitingInit, h.State);
            b.Deliver(Init());
            Assert.AreEqual(LaunchState.Connected, h.State);
        }

        [Test]
        public void InitWithOtherProtocolVersion_IsIgnored()
        {
            var b = new FakeBridge { IsHostMode = true };
            var h = new HostHandshake(b);
            h.Begin(0);
            b.Deliver(Init(v: 2));
            Assert.AreEqual(LaunchState.WaitingInit, h.State);
        }

        [Test]
        public void InitBeforeBegin_IsIgnored()
        {
            var b = new FakeBridge { IsHostMode = true };
            var h = new HostHandshake(b);
            b.Deliver(Init());
            Assert.AreEqual(LaunchState.Idle, h.State);
            Assert.IsNull(h.Init);
        }
    }
}
