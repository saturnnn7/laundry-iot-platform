# User stories

Format: As a role, I want X, so that Y. The use case in brackets has the detailed flow.

## Location Admin
- US-01 As a location admin, I want to register and create my location with a time zone, so that I can start managing machines. [UC-01]
- US-02 As a location admin, I want to add machines and link each to a device, so that the platform tracks their real state. [UC-01]
- US-03 As a location admin, I want to define programs with duration and price, so that residents can choose what to run. [UC-01]
- US-04 As a location admin, I want to create invite codes for residents and technicians, so that only the right people join. [UC-01]
- US-05 As a location admin, I want to set booking limits and hold prices, so that the rules fit my laundry. [UC-03, UC-04]
- US-06 As a location admin, I want to see all machines with their current state, so that I know what is working. [UC-07]
- US-07 As a location admin, I want a utilization report per machine, so that I can plan capacity. [UC-07]
- US-08 As a location admin, I want to see open faults and their ETA, so that I can inform residents. [UC-05, UC-07]

## Resident
- US-09 As a resident, I want to join my laundry with an invite code, so that I see only my machines. [UC-01]
- US-10 As a resident, I want to see which machines are free right now, so that I do not walk to a busy laundry. [UC-02]
- US-11 As a resident, I want to see free time slots for a day, so that I can plan my laundry. [UC-02]
- US-12 As a resident, I want to book a machine for a future slot with a program, so that it is ready when I arrive. [UC-03]
- US-13 As a resident, I want to cancel a booking before it starts, so that I do not block the machine for others. [UC-03]
- US-14 As a resident, I want to hold a free machine for 5, 10 or 15 minutes, so that nobody takes it while I bring my clothes. [UC-04]
- US-15 As a resident, I want to press a button on the machine and get a code, so that I can prove I am at the machine. [UC-08]
- US-16 As a resident, I want to enter the code in the app and have my name shown on the machine, so that I know I unlocked the right one. [UC-08]
- US-17 As a resident, I want to choose a program after unlocking, so that I run the cycle I need. [UC-08]
- US-18 As a resident, I want to be stopped from starting a cycle that would run into someone else's booking, so that nobody loses their slot. [UC-08]
- US-19 As a resident, I want to be told when my cycle is finished, so that I collect my clothes on time. [UC-06]
- US-20 As a resident, I want my booking moved to another machine when mine breaks, so that I still get my slot. [UC-05]
- US-21 As a resident, I want to see why a booking was moved or cancelled, so that I understand what happened. [UC-05]
- US-22 As a resident, I want to see my bookings and their status, so that I can manage them. [UC-03]

## Technician
- US-23 As a technician, I want to see open tickets for my locations, so that I know where to go. [UC-05]
- US-24 As a technician, I want to set the repair ETA, so that bookings are handled correctly. [UC-05]
- US-25 As a technician, I want to resolve a ticket, so that the machine returns to service. [UC-05]
- US-26 As a technician, I want to open a ticket manually, so that I can report a problem the machine did not detect. [UC-05]

## Device and platform
- US-27 As a device, I want to connect securely with my own certificate, so that no one can impersonate me. [UC-08]
- US-28 As a platform owner, I want machine state derived from telemetry, so that availability shows reality. [UC-02, UC-06]
- US-29 As a platform owner, I want a machine to show as offline when its device disconnects, so that nobody tries to use it. [UC-02]
- US-30 As a platform owner, I want every request authenticated with a JWT and scoped to the location, so that tenants cannot see each other's data. [UC-01]
