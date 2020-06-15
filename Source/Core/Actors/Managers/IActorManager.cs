﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Coyote.Actors
{
    /// <summary>
    /// Interface for managing an actor.
    /// </summary>
    internal interface IActorManager
    {
        /// <summary>
        /// True if the event handler of the actor is running, else false.
        /// </summary>
        bool IsEventHandlerRunning { get; set; }

        /// <summary>
        /// An optional operation associated with the current Event being handled.
        /// </summary>
        Operation CurrentOperation { get; set; }

        /// <summary>
        /// Returns the cached state of the actor.
        /// </summary>
        int GetCachedState();

        /// <summary>
        /// Checks if the specified event is currently ignored.
        /// </summary>
        bool IsEventIgnored(Event e, EventInfo eventInfo);

        /// <summary>
        /// Checks if the specified event is currently deferred.
        /// </summary>
        bool IsEventDeferred(Event e, EventInfo eventInfo);

        /// <summary>
        /// Checks if a default handler is currently available.
        /// </summary>
        bool IsDefaultHandlerAvailable();

        /// <summary>
        /// Notifies the actor that an event has been enqueued.
        /// </summary>
        void OnEnqueueEvent(Event e, Operation op, EventInfo eventInfo);

        /// <summary>
        /// Notifies the actor that an event has been raised.
        /// </summary>
        void OnRaiseEvent(Event e, Operation op, EventInfo eventInfo);

        /// <summary>
        /// Notifies the actor that it is waiting to receive an event of one of the specified types.
        /// </summary>
        void OnWaitEvent(IEnumerable<Type> eventTypes);

        /// <summary>
        /// Notifies the actor that an event it was waiting to receive has been enqueued.
        /// </summary>
        void OnReceiveEvent(Event e, Operation op, EventInfo eventInfo);

        /// <summary>
        /// Notifies the actor that an event it was waiting to receive was already in the
        /// event queue when the actor invoked the receive statement.
        /// </summary>
        void OnReceiveEventWithoutWaiting(Event e, Operation op, EventInfo eventInfo);

        /// <summary>
        /// Notifies the actor that an event has been dropped.
        /// </summary>
        void OnDropEvent(Event e, Operation op, EventInfo eventInfo);

        /// <summary>
        /// Asserts if the specified condition holds.
        /// </summary>
        void Assert(bool predicate, string s, object arg0);

        /// <summary>
        /// Asserts if the specified condition holds.
        /// </summary>
        void Assert(bool predicate, string s, object arg0, object arg1);

        /// <summary>
        /// Asserts if the specified condition holds.
        /// </summary>
        void Assert(bool predicate, string s, object arg0, object arg1, object arg2);

        /// <summary>
        /// Asserts if the specified condition holds.
        /// </summary>
        void Assert(bool predicate, string s, params object[] args);
    }
}
