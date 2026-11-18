# Glossary

- **Location** - a dormitory or laundry; the tenant boundary. Has a time zone and configuration.
- **Resource / machine** - a washer or dryer that can be reserved and monitored. The word machine is used in the API.
- **Device** - the controller of a machine, identified by `deviceId` and a certificate. In this project it is a simulator.
- **Program** - a wash or dry cycle type with a duration (multiple of 15 minutes) and a price.
- **Occupied time** - program minutes + 5 load + 5 unload, rounded up to 15 minutes. The time a reservation blocks.
- **Slot** - one 15-minute cell of the booking grid.
- **Scheduled booking** - a reservation of a machine for a future time.
- **Hold** - a short reservation (5, 10 or 15 minutes) starting now, at a fixed price.
- **Walk-up** - a session started at the machine with no prior booking or hold.
- **Session** - the use of a machine from claim to finished cycle; an Active booking.
- **Session request / code** - the 60-second code shown on the machine display after the button is pressed.
- **Claim** - entering the code in the API to bind yourself to the machine.
- **Ticket** - a fault record for a machine, with an optional technician ETA.
- **ETA** - the time the technician expects the machine to be repaired.
- **OutOfService** - a machine flag set by a ticket; blocks new reservations.
- **Telemetry** - periodic device readings; **event** - a one-off device message (button press).
- **Port** - an interface owned by the consumer module, with a stub implementation (payment, notifications).
