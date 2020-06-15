﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Coyote.Actors.Mocks
{
    /// <summary>
    /// Implements a queue of events that is used during testing.
    /// </summary>
    internal sealed class MockEventQueue : IEventQueue
    {
        /// <summary>
        /// Manages the actor that owns this queue.
        /// </summary>
        private readonly IActorManager ActorManager;

        /// <summary>
        /// The actor that owns this queue.
        /// </summary>
        private readonly Actor Actor;

        /// <summary>
        /// The internal queue that contains events with their metadata.
        /// </summary>
        private readonly LinkedList<(Event e, Operation op, EventInfo info)> Queue;

        /// <summary>
        /// The raised event and its metadata, or null if no event has been raised.
        /// </summary>
        private (Event e, Operation op, EventInfo info) RaisedEvent;

        /// <summary>
        /// Map from the types of events that the owner of the queue is waiting to receive
        /// to an optional predicate. If an event of one of these types is enqueued, then
        /// if there is no predicate, or if there is a predicate and evaluates to true, then
        /// the event is received, else the event is deferred.
        /// </summary>
        private Dictionary<Type, Func<Event, bool>> EventWaitTypes;

        /// <summary>
        /// Task completion source that contains the event obtained using an explicit receive.
        /// </summary>
        private TaskCompletionSource<Event> ReceiveCompletionSource;

        /// <summary>
        /// Checks if the queue is accepting new events.
        /// </summary>
        private bool IsClosed;

        /// <summary>
        /// The size of the queue.
        /// </summary>
        public int Size => this.Queue.Count;

        /// <summary>
        /// Checks if an event has been raised.
        /// </summary>
        public bool IsEventRaised => this.RaisedEvent != default;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockEventQueue"/> class.
        /// </summary>
        internal MockEventQueue(IActorManager actorManager, Actor actor)
        {
            this.ActorManager = actorManager;
            this.Actor = actor;
            this.Queue = new LinkedList<(Event, Operation, EventInfo)>();
            this.EventWaitTypes = new Dictionary<Type, Func<Event, bool>>();
            this.IsClosed = false;
        }

        /// <inheritdoc/>
        public EnqueueStatus Enqueue(Event e, Operation op, EventInfo info)
        {
            if (this.IsClosed)
            {
                return EnqueueStatus.Dropped;
            }

            if (this.EventWaitTypes.TryGetValue(e.GetType(), out Func<Event, bool> predicate) &&
                (predicate is null || predicate(e)))
            {
                this.EventWaitTypes.Clear();
                this.ActorManager.OnReceiveEvent(e, op, info);
                this.ReceiveCompletionSource.SetResult(e);
                return EnqueueStatus.EventHandlerRunning;
            }

            this.ActorManager.OnEnqueueEvent(e, op, info);
            this.Queue.AddLast((e, op, info));

            if (info.Assert >= 0)
            {
                var eventCount = this.Queue.Count(val => val.e.GetType().Equals(e.GetType()));
                this.ActorManager.Assert(eventCount <= info.Assert,
                    "There are more than {0} instances of '{1}' in the input queue of {2}.",
                    info.Assert, info.EventName, this.Actor.Id);
            }

            if (!this.ActorManager.IsEventHandlerRunning)
            {
                if (this.TryDequeueEvent(true).e is null)
                {
                    return EnqueueStatus.NextEventUnavailable;
                }
                else
                {
                    this.ActorManager.IsEventHandlerRunning = true;
                    return EnqueueStatus.EventHandlerNotRunning;
                }
            }

            return EnqueueStatus.EventHandlerRunning;
        }

        /// <inheritdoc/>
        public (DequeueStatus status, Event e, Operation op, EventInfo info) Dequeue()
        {
            // Try to get the raised event, if there is one. Raised events
            // have priority over the events in the inbox.
            if (this.RaisedEvent != default)
            {
                if (this.ActorManager.IsEventIgnored(this.RaisedEvent.e, this.RaisedEvent.info))
                {
                    // TODO: should the user be able to raise an ignored event?
                    // The raised event is ignored in the current state.
                    this.RaisedEvent = default;
                }
                else
                {
                    (Event e, Operation op, EventInfo info) raisedEvent = this.RaisedEvent;
                    this.RaisedEvent = default;
                    return (DequeueStatus.Raised, raisedEvent.e, raisedEvent.op, raisedEvent.info);
                }
            }

            var hasDefaultHandler = this.ActorManager.IsDefaultHandlerAvailable();
            if (hasDefaultHandler)
            {
                this.Actor.Runtime.NotifyDefaultEventHandlerCheck(this.Actor);
            }

            // Try to dequeue the next event, if there is one.
            (Event e, Operation op, EventInfo info) dequeued = this.TryDequeueEvent();
            if (dequeued.e != null)
            {
                // Found next event that can be dequeued.
                return (DequeueStatus.Success, dequeued.e, dequeued.op, dequeued.info);
            }

            // No event can be dequeued, so check if there is a default event handler.
            if (!hasDefaultHandler)
            {
                // There is no default event handler installed, so do not return an event.
                this.ActorManager.IsEventHandlerRunning = false;
                this.NotifyQuiescent();
                return (DequeueStatus.NotAvailable, null, null, null);
            }

            // TODO: check op-id of default event.
            // A default event handler exists.
            string stateName = this.Actor is StateMachine stateMachine ?
                NameResolver.GetStateNameForLogging(stateMachine.CurrentState) : string.Empty;
            var eventOrigin = new EventOriginInfo(this.Actor.Id, this.Actor.GetType().FullName, stateName);
            return (DequeueStatus.Default, DefaultEvent.Instance, null, new EventInfo(DefaultEvent.Instance, eventOrigin));
        }

        /// <summary>
        /// Notify caller if they are waiting for actor to reach quiescent state.
        /// </summary>
        private void NotifyQuiescent()
        {
            var q = this.ActorManager.CurrentOperation as QuiescentOperation;
            if (q != null && !q.IsCompleted)
            {
                q.TrySetResult(true);
            }
        }

        /// <summary>
        /// Dequeues the next event and its metadata, if there is one available, else returns null.
        /// </summary>
        private (Event e, Operation op, EventInfo info) TryDequeueEvent(bool checkOnly = false)
        {
            (Event, Operation, EventInfo) nextAvailableEvent = default;

            // Iterates through the events and metadata in the inbox.
            var node = this.Queue.First;
            while (node != null)
            {
                var nextNode = node.Next;
                var currentEvent = node.Value;

                if (this.ActorManager.IsEventIgnored(currentEvent.e, currentEvent.info))
                {
                    if (!checkOnly)
                    {
                        // Removes an ignored event.
                        this.Queue.Remove(node);
                    }

                    node = nextNode;
                    continue;
                }

                // Skips a deferred event.
                if (!this.ActorManager.IsEventDeferred(currentEvent.e, currentEvent.info))
                {
                    nextAvailableEvent = currentEvent;
                    if (!checkOnly)
                    {
                        this.Queue.Remove(node);
                    }

                    break;
                }

                node = nextNode;
            }

            return nextAvailableEvent;
        }

        /// <inheritdoc/>
        public void RaiseEvent(Event e, Operation op)
        {
            string stateName = this.Actor is StateMachine stateMachine ?
                NameResolver.GetStateNameForLogging(stateMachine.CurrentState) : string.Empty;
            var eventOrigin = new EventOriginInfo(this.Actor.Id, this.Actor.GetType().FullName, stateName);
            var info = new EventInfo(e, eventOrigin);
            this.RaisedEvent = (e, op, info);
            this.ActorManager.OnRaiseEvent(e, op, info);
        }

        /// <inheritdoc/>
        public Task<Event> ReceiveEventAsync(Type eventType, Func<Event, bool> predicate = null)
        {
            var eventWaitTypes = new Dictionary<Type, Func<Event, bool>>
            {
                { eventType, predicate }
            };

            return this.ReceiveEventAsync(eventWaitTypes);
        }

        /// <inheritdoc/>
        public Task<Event> ReceiveEventAsync(params Type[] eventTypes)
        {
            var eventWaitTypes = new Dictionary<Type, Func<Event, bool>>();
            foreach (var type in eventTypes)
            {
                eventWaitTypes.Add(type, null);
            }

            return this.ReceiveEventAsync(eventWaitTypes);
        }

        /// <inheritdoc/>
        public Task<Event> ReceiveEventAsync(params Tuple<Type, Func<Event, bool>>[] events)
        {
            var eventWaitTypes = new Dictionary<Type, Func<Event, bool>>();
            foreach (var e in events)
            {
                eventWaitTypes.Add(e.Item1, e.Item2);
            }

            return this.ReceiveEventAsync(eventWaitTypes);
        }

        /// <summary>
        /// Waits for an event to be enqueued.
        /// </summary>
        private Task<Event> ReceiveEventAsync(Dictionary<Type, Func<Event, bool>> eventWaitTypes)
        {
            this.Actor.Runtime.NotifyReceiveCalled(this.Actor);

            (Event e, Operation op, EventInfo info) receivedEvent = default;
            var node = this.Queue.First;
            while (node != null)
            {
                // Dequeue the first event that the caller waits to receive, if there is one in the queue.
                if (eventWaitTypes.TryGetValue(node.Value.e.GetType(), out Func<Event, bool> predicate) &&
                    (predicate is null || predicate(node.Value.e)))
                {
                    receivedEvent = node.Value;
                    this.Queue.Remove(node);
                    break;
                }

                node = node.Next;
            }

            if (receivedEvent == default)
            {
                this.ReceiveCompletionSource = new TaskCompletionSource<Event>();
                this.EventWaitTypes = eventWaitTypes;
                this.ActorManager.OnWaitEvent(this.EventWaitTypes.Keys);
                return this.ReceiveCompletionSource.Task;
            }

            this.ActorManager.OnReceiveEventWithoutWaiting(receivedEvent.e, receivedEvent.op, receivedEvent.info);
            return Task.FromResult(receivedEvent.e);
        }

        /// <inheritdoc/>
        public int GetCachedState()
        {
            unchecked
            {
                var hash = 19;
                foreach (var (_, _, info) in this.Queue)
                {
                    hash = (hash * 31) + info.EventName.GetHashCode();
                    if (info.HashedState != 0)
                    {
                        // Adds the user-defined hashed event state.
                        hash = (hash * 31) + info.HashedState;
                    }
                }

                return hash;
            }
        }

        /// <inheritdoc/>
        public void Close()
        {
            this.IsClosed = true;
        }

        /// <summary>
        /// Disposes the queue resources.
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            foreach (var (e, opGroupId, info) in this.Queue)
            {
                this.ActorManager.OnDropEvent(e, opGroupId, info);
            }

            this.Queue.Clear();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
