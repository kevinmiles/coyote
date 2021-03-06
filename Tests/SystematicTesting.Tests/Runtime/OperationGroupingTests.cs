﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Coyote.Actors;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Coyote.SystematicTesting.Tests.Runtime
{
    public class OperationGroupingTests : BaseSystematicTest
    {
        public OperationGroupingTests(ITestOutputHelper output)
            : base(output)
        {
        }

        private static Guid OperationGroup1 = Guid.NewGuid();
        private static Guid OperationGroup2 = Guid.NewGuid();

        private class E : Event
        {
            public ActorId Id;

            public E()
            {
            }

            public E(ActorId id)
            {
                this.Id = id;
            }
        }

        private class M1 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                var id = this.OperationGroupId;
                this.Assert(id == Guid.Empty, $"Operation group id is not '{Guid.Empty}', but {id}.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingSingleMachineNoSend()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M1));
            });
        }

        private class M2 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventDoAction(typeof(E), nameof(CheckEvent))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                this.SendEvent(this.Id, new E());
            }

            private void CheckEvent()
            {
                var id = this.OperationGroupId;
                this.Assert(id == Guid.Empty, $"Operation group id is not '{Guid.Empty}', but {id}.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingSingleMachineSend()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M2));
            });
        }

        private class M3 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventDoAction(typeof(E), nameof(CheckEvent))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                this.Runtime.SendEvent(this.Id, new E(), OperationGroup1);
            }

            private void CheckEvent()
            {
                var id = this.OperationGroupId;
                this.Assert(id == OperationGroup1, $"Operation group id is not '{OperationGroup1}', but {id}.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingSingleMachineSendStarter()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M3));
            });
        }

        private class M4A : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                this.CreateActor(typeof(M4B));
            }
        }

        private class M4B : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                var id = this.OperationGroupId;
                this.Assert(id == Guid.Empty, $"Operation group id is not '{Guid.Empty}', but {id}.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingTwoMachinesCreate()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M4A));
            });
        }

        private class M5A : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                var target = this.CreateActor(typeof(M5B));
                this.SendEvent(target, new E());
            }
        }

        private class M5B : StateMachine
        {
            [Start]
            [OnEventDoAction(typeof(E), nameof(CheckEvent))]
            private class Init : State
            {
            }

            private void CheckEvent()
            {
                var id = this.OperationGroupId;
                this.Assert(id == Guid.Empty, $"Operation group id is not '{Guid.Empty}', but {id}.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingTwoMachinesSend()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M5A));
            });
        }

        private class M6A : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                var target = this.CreateActor(typeof(M6B));
                this.Runtime.SendEvent(target, new E(), OperationGroup1);
            }
        }

        private class M6B : StateMachine
        {
            [Start]
            [OnEventDoAction(typeof(E), nameof(CheckEvent))]
            private class Init : State
            {
            }

            private void CheckEvent()
            {
                var id = this.OperationGroupId;
                this.Assert(id == OperationGroup1, $"Operation group id is not '{OperationGroup1}', but {id}.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingTwoMachinesSendStarter()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M6A));
            });
        }

        private class M7A : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventDoAction(typeof(E), nameof(CheckEvent))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                var target = this.CreateActor(typeof(M7B));
                this.Runtime.SendEvent(target, new E(this.Id), OperationGroup1);
            }

            private void CheckEvent()
            {
                var id = this.OperationGroupId;
                this.Assert(id == OperationGroup1, $"Operation group id is not '{OperationGroup1}', but {id}.");
            }
        }

        private class M7B : StateMachine
        {
            [Start]
            [OnEventDoAction(typeof(E), nameof(CheckEvent))]
            private class Init : State
            {
            }

            private void CheckEvent(Event e)
            {
                var id = this.OperationGroupId;
                this.Assert(id == OperationGroup1, $"Operation group id is not '{OperationGroup1}', but {id}.");
                this.SendEvent((e as E).Id, new E());
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingTwoMachinesSendBack()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M7A));
            });
        }

        private class M8A : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventDoAction(typeof(E), nameof(CheckEvent))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                var target = this.CreateActor(typeof(M8B));
                this.Runtime.SendEvent(target, new E(this.Id), OperationGroup1);
            }

            private void CheckEvent()
            {
                var id = this.OperationGroupId;
                this.Assert(id == OperationGroup2, $"Operation group id is not '{OperationGroup2}', but {id}.");
            }
        }

        private class M8B : StateMachine
        {
            [Start]
            [OnEventDoAction(typeof(E), nameof(CheckEvent))]
            private class Init : State
            {
            }

            private void CheckEvent(Event e)
            {
                var id = this.OperationGroupId;
                this.Assert(id == OperationGroup1, $"Operation group id is not '{OperationGroup1}', but {id}.");
                this.Runtime.SendEvent((e as E).Id, new E(), OperationGroup2);
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingTwoMachinesSendBackStarter()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M8A));
            });
        }

        private class M9A : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventDoAction(typeof(E), nameof(CheckEvent))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                var target = this.CreateActor(typeof(M9B));
                this.Runtime.SendEvent(target, new E(this.Id), OperationGroup1);
            }

            private void CheckEvent()
            {
                var id = this.OperationGroupId;
                this.Assert(id == OperationGroup2, $"Operation group id is not '{OperationGroup2}', but {id}.");
            }
        }

        private class M9B : StateMachine
        {
            [Start]
            [OnEventDoAction(typeof(E), nameof(CheckEvent))]
            private class Init : State
            {
            }

            private void CheckEvent(Event e)
            {
                this.CreateActor(typeof(M9C));
                var id = this.OperationGroupId;
                this.Assert(id == OperationGroup1, $"Operation group id is not '{OperationGroup1}', but {id}.");
                this.Runtime.SendEvent((e as E).Id, new E(), OperationGroup2);
            }
        }

        private class M9C : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                var id = this.OperationGroupId;
                this.Assert(id == OperationGroup1, $"Operation group id is not '{OperationGroup1}', but {id}.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingThreeMachinesSendStarter()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M9A));
            });
        }

        private class M10 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventGotoState(typeof(E), typeof(Final))]
            private class Init : State
            {
            }

            [OnEntry(nameof(FinalOnEntry))]
            [OnEventDoAction(typeof(E), nameof(Check))]
            private class Final : State
            {
            }

            private void InitOnEntry()
            {
                var e = new E(this.Id);
                this.SendEvent(this.Id, e, OperationGroup1);
                this.Runtime.SendEvent(this.Id, e, OperationGroup2);
            }

            private void FinalOnEntry()
            {
                this.Assert(this.OperationGroupId == OperationGroup1,
                    $"[1] Operation group id is not '{OperationGroup1}', but {this.OperationGroupId}.");
            }

            private void Check()
            {
                this.Assert(this.OperationGroupId == OperationGroup2,
                    $"[2] Operation group id is not '{OperationGroup2}', but {this.OperationGroupId}.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingSendSameEventWithOtherOpId()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M10));
            });
        }

        private class M11 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventGotoState(typeof(E), typeof(Final))]
            private class Init : State
            {
            }

            [OnEntry(nameof(FinalOnEntry))]
            [OnEventDoAction(typeof(E), nameof(Check))]
            private class Final : State
            {
            }

            private void InitOnEntry()
            {
                var e = new E(this.Id);
                this.SendEvent(this.Id, e, OperationGroup1);
                this.OperationGroupId = OperationGroup2;
                this.Runtime.SendEvent(this.Id, e);
            }

            private void FinalOnEntry()
            {
                this.Assert(this.OperationGroupId == OperationGroup1,
                    $"[1] Operation group id is not '{OperationGroup1}', but {this.OperationGroupId}.");
            }

            private void Check()
            {
                this.Assert(this.OperationGroupId == OperationGroup2,
                    $"[2] Operation group id is not '{OperationGroup2}', but {this.OperationGroupId}.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingSendSameEventWithOtherMachineOpId()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M11));
            });
        }

        private class M12 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private async Task InitOnEntry()
            {
                this.SendEvent(this.Id, new E(this.Id), OperationGroup1);
                this.Assert(this.OperationGroupId == Guid.Empty,
                    $"[1] Operation group id is not '{Guid.Empty}', but {this.OperationGroupId}.");
                await this.ReceiveEventAsync(typeof(E));
                this.Assert(this.OperationGroupId == OperationGroup1,
                    $"[2] Operation group id is not '{OperationGroup1}', but {this.OperationGroupId}.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestOperationGroupingReceivedEvent()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(M12));
            });
        }
    }
}
