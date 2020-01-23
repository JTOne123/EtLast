﻿namespace FizzCode.EtLast.Diagnostics.Interface
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public delegate void OnEventAddedDelegate(Playbook playbook, List<AbstractEvent> abstractEvents);
    public delegate void OnProcessAddedDelegate(Playbook playbook, TrackedProcess process);
    public delegate void OnCountersUpdatedDelegate(Playbook playbook);

    public class Playbook
    {
        public ExecutionContext ExecutionContext { get; }

        public List<AbstractEvent> Events { get; } = new List<AbstractEvent>();
        public Dictionary<int, TrackedRow> RowList { get; } = new Dictionary<int, TrackedRow>();
        public Dictionary<string, TrackedStore> StoreList { get; } = new Dictionary<string, TrackedStore>();
        public Dictionary<int, TrackedProcess> ProcessList { get; } = new Dictionary<int, TrackedProcess>();
        public Dictionary<string, Counter> Counters { get; } = new Dictionary<string, Counter>();

        public OnProcessAddedDelegate OnProcessAdded { get; set; }
        public OnCountersUpdatedDelegate OnCountersUpdated { get; set; }
        public OnEventAddedDelegate OnEventsAdded { get; set; }

        public Playbook(ExecutionContext sessionContext)
        {
            ExecutionContext = sessionContext;
        }

        public void AddEvents(List<AbstractEvent> abstactEvents)
        {
            var newEvents = new List<AbstractEvent>();

            ContextCountersUpdatedEvent lastContextCountersUpdatedEvent = null;
            foreach (var abstactEvent in abstactEvents)
            {
                switch (abstactEvent)
                {
                    case LogEvent evt:
                        {
                            if (evt.ProcessUid != null && !ProcessList.TryGetValue(evt.ProcessUid.Value, out var process))
                                continue;
                        }
                        break;
                    case ContextCountersUpdatedEvent evt:
                        lastContextCountersUpdatedEvent = evt;
                        break;
                    case ProcessCreatedEvent evt:
                        {
                            if (!ProcessList.TryGetValue(evt.Uid, out var process))
                            {
                                process = new TrackedProcess(evt.Uid, evt.Type, evt.Name);
                                ProcessList.Add(process.Uid, process);
                                OnProcessAdded?.Invoke(this, process);
                            }
                        }
                        break;
                    case RowCreatedEvent evt:
                        {
                            if (!ProcessList.TryGetValue(evt.ProcessUid, out var process))
                                continue;

                            var row = new TrackedRow()
                            {
                                Uid = evt.RowUid,
                                CreatedByEvent = evt,
                            };

                            row.AllEvents.Add(evt);

                            foreach (var value in evt.Values)
                            {
                                if (value.Value != null)
                                {
                                    row.Values[value.Name] = value;
                                }
                            }

                            RowList[row.Uid] = row;

                            process.AddRow(row);
                        }
                        break;
                    case RowOwnerChangedEvent evt:
                        {
                            if (!ProcessList.TryGetValue(evt.PreviousProcessUid, out var previousProcess))
                                continue;

                            if (!RowList.TryGetValue(evt.RowUid, out var row))
                                continue;

                            TrackedProcess newProcess = null;
                            if (evt.NewProcessUid != null && !ProcessList.TryGetValue(evt.NewProcessUid.Value, out newProcess))
                                continue;

                            if (newProcess != null)
                            {
                                previousProcess.RemoveRow(row);
                                newProcess.AddRow(row);
                            }
                            else
                            {
                                previousProcess.DropRow(row);
                            }

                            row.AllEvents.Add(evt);
                        }
                        break;
                    case RowValueChangedEvent evt:
                        {
                            if (!RowList.TryGetValue(evt.RowUid, out var row))
                                continue;

                            if (evt.ProcessUid != null && !ProcessList.TryGetValue(evt.ProcessUid.Value, out var process))
                                continue;

                            row.AllEvents.Add(evt);
                            if (evt.CurrentValue != null)
                            {
                                row.Values[evt.Column] = evt.CurrentValue;
                            }
                            else
                            {
                                row.Values.Remove(evt.Column);
                            }
                        }
                        break;
                    case RowStoredEvent evt:
                        {
                            if (!RowList.TryGetValue(evt.RowUid, out var row))
                                continue;

                            if (!ProcessList.TryGetValue(evt.ProcessUid, out var process))
                                continue;

                            row.AllEvents.Add(evt);
                            var storePath = string.Join("/", evt.Locations.Select(x => x.Value));
                            if (!StoreList.TryGetValue(storePath, out var store))
                            {
                                store = new TrackedStore(storePath);
                                StoreList.Add(storePath, store);
                            }

                            var snapshot = row.GetSnapshot();
                            store.Rows.Add(new Tuple<RowStoredEvent, TrackedRowSnapshot>(evt, snapshot));
                        }
                        break;
                }

                newEvents.Add(abstactEvent);
                Events.Add(abstactEvent);
            }

            if (newEvents.Count == 0)
                return;

            OnEventsAdded?.Invoke(this, newEvents);

            if (lastContextCountersUpdatedEvent != null)
            {
                foreach (var counter in lastContextCountersUpdatedEvent.Counters)
                {
                    Counters[counter.Name] = counter;
                }

                OnCountersUpdated?.Invoke(this);
            }
        }
    }
}