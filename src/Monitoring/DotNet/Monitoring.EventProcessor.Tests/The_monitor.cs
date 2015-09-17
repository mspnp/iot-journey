﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Practices.IoTJourney.Monitoring.EventProcessor;
using Microsoft.Reactive.Testing;
using Microsoft.ServiceBus.Messaging;
using Xunit;

namespace Monitoring.EventProcessor.Tests
{
    public class The_monitor
    {
        private readonly List<PartitionSnapshot> _captured = new List<PartitionSnapshot>();
        private readonly EventHubMonitor _monitor;
        private readonly string[] _partitionIds = { "0", "1", "2" };
        private readonly TestScheduler _virtualTime = new TestScheduler();

        public The_monitor()
        {
            _monitor = new EventHubMonitor(
                _partitionIds,
                partitionId => Task.FromResult(new PartitionCheckpoint()),
                partitionId => Task.FromResult(new PartitionDescription("myHub", "0")),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                _virtualTime);
        }

        [Fact]
        public void should_emit_an_event_for_each_partition()
        {
            using (_monitor.Subscribe(_captured.Add))
            {
                var enoughForAllPartitions = TimeSpan.FromSeconds(_partitionIds.Length);
                _virtualTime.AdvanceBy(enoughForAllPartitions.Ticks);
            }

            Assert.Equal(_partitionIds.Length, _captured.Count);

            for (var i = 0; i < _captured.Count; i++)
            {
                Assert.Equal(_partitionIds[i], _captured[i].PartitionId);
            }
        }

        public class when_an_exception_is_thrown
        {
            private static EventHubMonitor CreateMonitorThrowing(Exception e, TestScheduler scheduler)
            {
                return new EventHubMonitor(
                    new[] { "0" },
                    partitionId => { throw e; },
                    partitionId => Task.FromResult(new PartitionDescription("myHub", "0")),
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(1),
                    scheduler);
            }

            [Fact]
            public void should_not_stop_the_stream_for_a_timeout_exception()
            {
                var wasErrorThrown = false;

                var virtualTime = new TestScheduler();
                var monitor = CreateMonitorThrowing(
                    new TimeoutException(""),
                    virtualTime);

                monitor.Subscribe(
                    onNext: _ => { },
                    onError: e => { wasErrorThrown = true; });

                virtualTime.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

                Assert.False(wasErrorThrown);
            }

            [Fact]
            public void should_not_stop_the_stream_for_a_messaging_communication_exception()
            {
                var wasErrorThrown = false;

                var virtualTime = new TestScheduler();
                var monitor = CreateMonitorThrowing(
                    new MessagingCommunicationException(""), 
                    virtualTime);

                monitor.Subscribe(
                    onNext: _ => { },
                    onError: e => { wasErrorThrown = true; });

                virtualTime.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

                Assert.False(wasErrorThrown);
            }

            [Fact]
            public void should_stop_the_stream_for_exceptions_in_general()
            {
                var wasErrorThrown = false;

                var virtualTime = new TestScheduler();
                var monitor = CreateMonitorThrowing(
                    new Exception(""), 
                    virtualTime);

                monitor.Subscribe(
                    onNext: _ => { },
                    onError: e => { wasErrorThrown = true; });

                virtualTime.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

                Assert.True(wasErrorThrown);
            }
        }
    }
}