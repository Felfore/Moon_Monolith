# Event Creation Template & Guide

This guide provides a comprehensive template for creating new custom events or overriding existing ones within the `Moonstation` hierarchy. Each field is documented based on the audited C# component definitions.

## Configuration Components

### 1. `StationEvent` Component
This is the core component for all events. It handles timing, announcements, and player-count eligibility.

*   **Weight**: Relative probability. `5.0` is low, `10.0` is normal, `15.0` is high.
*   **Timing**:
    *   `EarliestStart`: Minutes into the round before it can trigger.
    *   `ReoccurrenceDelay`: Minutes before the same event can repeat.
    *   `Duration`: How long the event logic remains "active".
    *   `WarningDurationLeft`: (Frontier) Seconds between the initial warning and the official start.
*   **Announcements**:
    *   `startAnnouncement`: Text sent to all players at start.
    *   `warningAnnouncement`: Text sent during the warning phase.
    *   `startRadioAnnouncement`: Text sent via a specific radio channel.
    *   `startRadioAnnouncementChannel`: The `ProtoId` of the channel (e.g., `Supply`, `Engineering`).
*   **Eligibility**:
    *   `minimumPlayers`: Hard floor for station population.
    *   `requiredJobs`: A dictionary of `JobId: Count` that must be online.

### 2. `GameRule` Component
Applies to all game rules, including events.

*   **Delay**: A random `min/max` (seconds) before the logic starts *after* the rule is added.
*   **MinPlayers**: If `CancelPresetOnTooFewPlayers` is true, this can cancel the entire preset at roundstart.
*   **NumberOfGrids**: (Mono) Defines how many grids the event should target or spawn.

### 3. `BluespaceErrorRule` Component (Frontier/POI)
Used for events that spawn grids (Dungeons, Vaults, Salvage).

*   **Groups**: Dictionary of spawn groups.
    *   `!type:BluespaceGridSpawnGroup`: For spawning specific map files.
    *   `!type:BluespaceDungeonSpawnGroup`: For spawning procedurally generated dungeons.
*   **RewardAccounts**: Specifies which sector accounts (e.g., `Nfsd`) get a cut of the grid's value upon completion.

---

## Complete Blueprint Example

```yaml
- type: entity
  id: MoonExampleEvent            # Unique ID for our new event
  parent: ExampleBaseEvent        # Inherit from an existing event for base logic
  components:
  - type: StationEvent
    weight: 10                    # Relative probability of selection (higher = more common)
    earliestStart: 30             # Minimum round time (minutes) before this can trigger
    reoccurrenceDelay: 60         # Minimum time (minutes) before this event can repeat

    # -- Timing (TimeSpan format or seconds) --
    duration: 120                 # Duration (seconds or HH:MM:SS) of the active event phase
    maxDuration: 240              # Max duration if there is randomness involved
    warningDurationLeft: 300      # (Frontier) Delay (seconds) between Warning and Start

    # -- Announcements --
    startAnnouncement: "Example event starting."
    warningAnnouncement: "Example event is imminent!" # (Frontier)
    endAnnouncement: "Example event has concluded."
    startAnnouncementColor: "#18abf5" # Custom hex color for the announcement

    # -- Radio Announcements (e.g. for Salvage/Dungeons) --
    startRadioAnnouncement: "station-event-example-start"
    startRadioAnnouncementChannel: Supply # (Supply, Engineering, Medical, etc.)

    # -- Audio --
    startAudio:
      path: /Audio/Announcements/attention.ogg
      params:
        volume: -4
    warningAudio: /Audio/Announcements/notice.ogg # (Frontier)

    # -- Player & Job Restrictions --
    minimumPlayers: 10            # (StationEvent) Won't trigger if pop is lower
    maximumPlayers: 100           # (StationEvent) Won't trigger if pop is higher (default 999)
    requiredJobs:                 # (Frontier) Won't trigger unless these jobs are filled
      Captain: 1
      NfsdOfficer: 1

  - type: GameRule
    delay:                        # Random delay (seconds) before the logic actually starts
      min: 10
      max: 30
    minPlayers: 5                 # (GameRule) Hard floor to even allow the rule to add
    numberOfGrids:                # (Mono) Min/Max grids affected (if applicable)
      min: 1
      max: 3

  - type: BluespaceErrorRule      # (Frontier) For grid-spawning/POI events
    anchorAfterWarp: true
    deleteGridsOnEnd: true
    extendIfPopulated: true       # Prevents grid deletion while players are present
    rewardAccounts:               # (Frontier) Accounts credited on completion
      Nfsd: 1.0
    groups:                       # Grids to spawn
      grid: !type:BluespaceGridSpawnGroup
        nameLoc: ["Unidentified Signal"]
        minimumDistance: 1500
        maximumDistance: 5000
        paths: ["/Maps/_Moonstation/Example.yml"]
```
