# State machines and key sequences (Mermaid)

## Booking
```mermaid
stateDiagram-v2
    [*] --> Requested
    Requested --> Confirmed : payment port ok
    Requested --> Cancelled : payment failed
    Confirmed --> Active : session started
    Confirmed --> Cancelled : owner cancels, or machine fault with no replacement
    Confirmed --> NoShow : scheduled, not claimed in time
    Confirmed --> Expired : hold time over
    Active --> Completed : cycle finished
    Active --> Cancelled : machine fault
    Confirmed --> Confirmed : moved to another machine or time (change log entry)
```

## Session request (code)
```mermaid
stateDiagram-v2
    [*] --> CodeIssued : buttonPressed
    CodeIssued --> Claimed : valid code entered
    CodeIssued --> Expired : 60 seconds pass
    CodeIssued --> Locked : too many failed attempts
    Claimed --> Started : program chosen, start acked
    Claimed --> Expired : not started in 5 minutes
```

## Machine cycle (Telemetry)
```mermaid
stateDiagram-v2
    [*] --> Unknown
    Unknown --> Idle : online and phase idle
    Idle --> Running : phase running
    Running --> Finished : phase finished
    Finished --> Idle : door opened
    Idle --> Fault : phase fault
    Running --> Fault : phase fault
    Fault --> Idle : phase idle
    Idle --> Unknown : offline
    Running --> Unknown : offline
```

## Ticket
```mermaid
stateDiagram-v2
    [*] --> Open : fault or manual
    Open --> EtaSet : technician sets ETA
    EtaSet --> EtaSet : ETA changed
    Open --> Resolved
    EtaSet --> Resolved
    Open --> Cancelled
    EtaSet --> Cancelled
```

## Resource operational state (derived for availability)
| Condition (first match wins) | State |
|---|---|
| OutOfService flag set | OutOfService |
| cycle phase running, or Active booking | InUse |
| a Hold or a Scheduled booking covers now (or starts within the claim window) | Reserved |
| otherwise | Available |

Cycle state Unknown adds `offline: true` to the availability item; the machine cannot be started until it is online.

## Session start (UC-08)
```mermaid
sequenceDiagram
    participant R as Resident app/API
    participant B as Backend
    participant D as Device
    D->>B: event buttonPressed
    B->>D: cmd showCode (code, validUntil)
    R->>B: POST /sessions/claim (code)
    B->>D: cmd unlock (displayName)
    D-->>B: ack ok
    R->>B: POST /sessions/{id}/start (programId)
    B->>D: cmd start (programId, minutes)
    D-->>B: ack ok
    D->>B: telemetry phase running ... finished
    B->>B: session Completed
```

## Fault and rescheduling (UC-05)
```mermaid
sequenceDiagram
    participant D as Device
    participant T as Telemetry
    participant M as Maintenance
    participant X as Resources
    participant B as Booking
    D->>T: telemetry phase fault
    T->>M: FaultDetected
    M->>M: open Ticket
    M->>X: TicketOpened
    X->>X: OutOfService
    M->>B: TicketOpened
    B->>B: process bookings starting in next 12 h
    M->>B: TicketEtaSet (technician)
    B->>B: process bookings before ETA
    M->>X: TicketResolved
    X->>X: returned to service
```
