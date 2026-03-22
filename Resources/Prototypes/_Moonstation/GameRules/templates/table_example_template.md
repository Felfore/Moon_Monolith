# Entity Table Creation Template & Guide

This guide details the types of Entity Table Selectors you can use to structure and balance event or item pools within the `Moonstation` hierarchy.

## Common Base Fields
All selectors inherit the following optional properties:

*   **Weight**: (Float) A relative probability weight used when this selector is a child inside a `GroupSelector`.
*   **Prob**: (Float/Double) The raw chance between 1.0 (100%) and 0.0 (0%) that this selector will trigger when drawn.
*   **Rolls**: (Integer) How many times this selector guarantees a draw/roll. Defaults to 1. 

---

## Selector Types

### 1. `!type:AllSelector`
Fires **all** of its `children`. Commonly used at the root of a scheduler's table to pass a list of Game Rules, allowing the `StationEvent` component (player count, timings) on each rule to determine its own eligibility.

### 2. `!type:GroupSelector`
Randomly picks exactly **one** of its `children`. It chooses based on the `weight` values provided by the children. Highly useful for standard weighted loot or event selection.

### 3. `!type:NestedSelector`
Used to point the current selection tree to another existing table prototype via `tableId`. Useful for reusing drop or event tables in multiple places.

### 4. `!type:EntSelector`
Selects a specific `id` (Entity Prototype ID). Can optionally have an `amount`.

---

## Complete Blueprint Example

```yaml
- type: entityTable
  id: MoonExampleEventsTable
  # Using an AllSelector at the top allows all children to be evaluated by their own StationEvent rules
  table: !type:AllSelector
    children:
      - !type:GroupSelector # Puts two events into a weighted pool where only 1 fires
        children:
          # This event has a normal chance of being selected
          - id: MoonEvent1
            weight: 10
            
          # This event is very rare (weight 2 vs weight 10 = ~16% chance)
          - id: MoonEventRare  
            weight: 2
            
          # We can also nest another entire table into this group!
          - !type:NestedSelector
            tableId: SomeOtherPreExistingTable
            weight: 5
            
      # This event rolls entirely independently of the GroupSelector above 
      # because it is a direct child of the root AllSelector, but it only has a 25% chance to run
      - id: MoonIndependentEvent
        prob: 0.25 
        
      # Fires 3 instances of a specific entity (like an item or mob) instead of an event rule
      - !type:EntSelector
        id: MoonCustomItemOrMob
        amount: 3
```
