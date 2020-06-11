﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Coyote.Actors;
using Microsoft.Coyote.Specifications;
using Microsoft.Coyote.Tasks;
using Microsoft.Coyote.Tests.Common.Events;
using Microsoft.VisualBasic;
using Xunit;
using Xunit.Abstractions;
using SystemTask = System.Threading.Tasks.Task;

namespace Microsoft.Coyote.Production.Tests.Actors
{
    public class CreateActorIdFromNameTests : BaseProductionTest
    {
        public CreateActorIdFromNameTests(ITestOutputHelper output)
            : base(output)
        {
        }

        private class SetupEvent : Event
        {
            internal TaskCompletionSource<bool> Completed = TaskCompletionSource.Create<bool>();
            internal int Count;

            public SetupEvent(int count = 1)
            {
                this.Count = count;
            }
        }

        private class CompletedEvent : Event
        {
        }

        private class TestMonitor : Monitor
        {
            private SetupEvent Setup;

            [Start]
            [OnEventDoAction(typeof(SetupEvent), nameof(OnSetup))]
            [OnEventDoAction(typeof(CompletedEvent), nameof(OnCompleted))]
            private class S1 : State
            {
            }

            private void OnSetup(Event e)
            {
                this.Setup = (SetupEvent)e;
            }

            private void OnCompleted()
            {
                this.Setup.Count--;
                if (this.Setup.Count == 0)
                {
                    this.Setup.Completed.SetResult(true);
                }
            }
        }

        private class M : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                this.Monitor<TestMonitor>(new CompletedEvent());
            }
        }

        [Fact(Timeout = 5000)]
        public void TestCreateActorIdFromName1()
        {
            this.Test(async r =>
            {
                var setup = new SetupEvent(2);
                r.RegisterMonitor<TestMonitor>();
                r.Monitor<TestMonitor>(setup);
                var m1 = r.CreateActor(typeof(M));
                var m2 = r.CreateActorIdFromName(typeof(M), "M");
                r.Assert(!m1.Equals(m2));
                r.CreateActor(m2, typeof(M));
                await setup.Completed.Task;
            },
            Configuration.Create().WithProductionMonitorEnabled());
        }

        [Fact(Timeout = 5000)]
        public void TestCreateActorIdFromName2()
        {
            this.Test(async r =>
            {
                var setup = new SetupEvent(2);
                r.RegisterMonitor<TestMonitor>();
                r.Monitor<TestMonitor>(setup);
                var m1 = r.CreateActorIdFromName(typeof(M), "M1");
                var m2 = r.CreateActorIdFromName(typeof(M), "M2");
                r.Assert(!m1.Equals(m2));
                r.CreateActor(m1, typeof(M));
                r.CreateActor(m2, typeof(M));
                await setup.Completed.Task;
            },
            Configuration.Create().WithProductionMonitorEnabled());
        }

        private class M2 : StateMachine
        {
            [Start]
            private class S : State
            {
            }

            protected override SystemTask OnHaltAsync(Event e)
            {
                this.Monitor<TestMonitor>(new CompletedEvent());
                return base.OnHaltAsync(e);
            }
        }

        private class M3 : StateMachine
        {
            [Start]
            private class S : State
            {
            }
        }

        [Fact(Timeout = 5000)]
        public void TestCreateActorIdFromName4()
        {
            this.TestWithError(r =>
            {
                var m3 = r.CreateActorIdFromName(typeof(M3), "M3");
                r.CreateActor(m3, typeof(M2));
            },
            expectedError: "Cannot bind actor id '' of type 'M3' to an actor of type 'M2'.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestCreateActorIdFromName5()
        {
            this.TestWithError(r =>
            {
                var m1 = r.CreateActorIdFromName(typeof(M2), "M2");
                r.CreateActor(m1, typeof(M2));
                r.CreateActor(m1, typeof(M2));
            },
            Configuration.Create().WithProductionMonitorEnabled(),
            expectedErrors: new string[]
            {
                "Actor id '' is used by an existing or previously halted actor.",
                "An actor with id '0' was already created in generation '0'. This typically occurs if either the actor id was created by another runtime instance, or if a actor id from a previous runtime generation was deserialized, but the current runtime has not increased its generation value."
            },
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestCreateActorIdFromName6()
        {
            this.TestWithError(r =>
            {
                var m = r.CreateActorIdFromName(typeof(M2), "M2");
                r.SendEvent(m, UnitEvent.Instance);
            },
            Configuration.Create().WithProductionMonitorEnabled(),
            expectedError: "Cannot send event 'Events.UnitEvent' to actor id '' that is not bound to an actor instance.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestCreateActorIdFromName7()
        {
            this.TestWithError(async r =>
            {
                var setup = new SetupEvent();
                r.RegisterMonitor<TestMonitor>();
                r.Monitor<TestMonitor>(setup);
                var m = r.CreateActorIdFromName(typeof(M2), "M2");
                r.CreateActor(m, typeof(M2));

                // Make sure that the state machine halts.
                r.SendEvent(m, HaltEvent.Instance);

                await setup.Completed.Task;

                // Trying to bring up a halted state machine.
                r.CreateActor(m, typeof(M2));
            },
            configuration: Configuration.Create().WithProductionMonitorEnabled(),
            expectedErrors: new string[]
            {
                "Actor id '' is used by an existing or previously halted actor.",
                "An actor with id '0' was already created in generation '0'. This typically occurs if either the actor id was created by another runtime instance, or if a actor id from a previous runtime generation was deserialized, but the current runtime has not increased its generation value."
            },
            replay: true);
        }

        private class E2 : Event
        {
            public ActorId Mid;

            public E2(ActorId id)
            {
                this.Mid = id;
            }
        }

        private class M4 : StateMachine
        {
            [Start]
            [OnEventDoAction(typeof(UnitEvent), nameof(Process))]
            private class S : State
            {
            }

            private void Process()
            {
                this.Monitor<TestMonitor>(new CompletedEvent());
            }
        }

        private class M5 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class S : State
            {
            }

            private void InitOnEntry(Event e)
            {
                var id = (e as E2).Mid;
                this.SendEvent(id, UnitEvent.Instance);
            }
        }

        [Fact(Timeout = 5000)]
        public void TestCreateActorIdFromName8()
        {
            var configuration = Configuration.Create().WithProductionMonitorEnabled();
            configuration.TestingIterations = 100;

            this.TestWithError(async r =>
            {
                var m = r.CreateActorIdFromName(typeof(M4), "M4");
                r.CreateActor(typeof(M5), new E2(m));
                await Task.Delay(10);
            },
            configuration,
            expectedError: "Cannot send event 'Events.UnitEvent' to actor id '' that is not bound to an actor instance.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestCreateActorIdFromName9()
        {
            this.Test(r =>
            {
                var m1 = r.CreateActorIdFromName(typeof(M4), "M4");
                var m2 = r.CreateActorIdFromName(typeof(M4), "M4");
                r.Assert(m1.Equals(m2));
            });
        }

        private class M6 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                var m = this.Runtime.CreateActorIdFromName(typeof(M4), "M4");
                this.CreateActor(m, typeof(M4), "friendly");
                var op = this.CurrentOperation as Operation<bool>;
                if (op != null)
                {
                    op.SetResult(true);
                }
            }
        }

        [Fact(Timeout = 5000)]
        public void TestCreateActorIdFromName10()
        {
            this.TestWithError(r =>
            {
                r.CreateActor(typeof(M6));
                r.CreateActor(typeof(M6));
            },
            expectedError: "Actor id '' is used by an existing or previously halted actor.",
            replay: true);
        }

        private class M7 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private async SystemTask InitOnEntry()
            {
                var op = new Operation<bool>();
                this.Runtime.CreateActor(typeof(M6), null, op);
                await op.Completion.Task;
                var m = this.Runtime.CreateActorIdFromName(typeof(M4), "M4");
                this.Runtime.SendEvent(m, UnitEvent.Instance);
            }
        }

        [Fact(Timeout = 5000)]
        public void TestCreateActorIdFromName11()
        {
            this.Test(async r =>
            {
                var setup = new SetupEvent();
                r.RegisterMonitor<TestMonitor>();
                r.Monitor<TestMonitor>(setup);
                r.CreateActor(typeof(M7));
                await setup.Completed.Task;
            }, Configuration.Create().WithProductionMonitorEnabled());
        }
    }
}
