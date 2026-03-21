# Scheduler Creation Template & Guide

This guide details the components and fields used for creating Station Event Schedulers under the `Moonstation` hierarchy. Schedulers determine **when** and **how frequently** your game rules and events start.

## Configuration Components

### 1. `BasicStationEventScheduler`
The standard scheduler used for consistent, randomly spaced events throughout the round.

*   **MinimumTimeUntilFirstEvent**: (Seconds) Exactly how long the scheduler will wait at round start before it can fire its very first event.
*   **MinMaxEventTiming**: (Minutes/Seconds) A `min, max` dict or list determining the randomly rolled delay between events.
*   **ScheduledGameRules**: Which `EntityTableSelector` this scheduler should draw events from (usually a `!type:NestedSelector` pointing to a table rule).

### 2. `RampingStationEventScheduler`
A specialized scheduler used for threat escalation. Events happen faster and more frequently as the round progresses.

*   **AverageChaos**: Average ending chaos modifier (higher = faster events by default). Max chaos naturally deviates from this.
*   **AverageEndTime**: (Minutes) The average time when the scheduler will stop increasing the chaos modifier. This dictates the "peak" intensity time of the round.
*   **ScheduledGameRules**: The entity table used (similar to the Basic scheduler).

---

## Basic Scheduler Blueprint Example

```yaml
- type: entity
  id: MoonExampleBasicScheduler
  parent: BaseGameRule             # Required base parent for all schedulers
  components:
  - type: BasicStationEventScheduler
    minimumTimeUntilFirstEvent: 300 # Wait 5 minutes before firing the first event
    minMaxEventTiming:
      min: 15 # Wait at least 15 seconds between events...
      max: 60 # ...And at most 1 minute between events.
    scheduledGameRules: !type:NestedSelector
      tableId: MoonExampleEventsTable # Points to the EntityTable containing the available events
```

## Ramping Scheduler Blueprint Example

```yaml
- type: entity
  id: MoonExampleRampingScheduler
  parent: BaseGameRule
  components:
  - type: RampingStationEventScheduler
    averageChaos: 15.0             # Determines how aggressive the end-game gets
    averageEndTime: 120.0          # Cap scaling around the 2 hour mark
    scheduledGameRules: !type:NestedSelector
      tableId: MoonExampleThreatsTable # High-threat events table
```
