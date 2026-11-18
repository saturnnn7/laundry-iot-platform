# Laundry IoT Platform - business context

## Problem
In dormitories and self-service laundries, residents walk to the laundry and find every machine busy or broken. Owners do not know how much the machines are used or when a machine has failed, so faults are found by complaints.

## Solution
A cloud platform with connected washers and dryers. Each machine reports its real state through an IoT device. Residents see which machines are free, book a slot or hold a machine for a few minutes, and start it by entering a code shown on the machine. When a machine faults, the platform marks it out of service, informs the technician, and moves or cancels the affected bookings automatically.

## Users
- **Resident** - finds, books, holds and starts machines.
- **Location Admin** - sets up the location, machines, programs and prices; invites residents and technicians; reviews utilization and faults.
- **Technician** - receives fault tickets, sets the repair ETA, resolves tickets.

## Value
- Residents: less waiting, fewer wasted trips, fair access.
- Owners: utilization data, early fault detection, less manual coordination.
- Revenue model: paid holds and prepaid slots (payment is a stub in the first version).

## Scope of this project
Backend with REST API, MQTT broker, database, device simulator, infrastructure as code, security (TLS, mTLS, JWT), documentation. Not included: real payments, mobile apps, a rich web frontend, real hardware.

## Scale used for planning
1000 devices and 300 customers (a customer is a tenant, one Location Admin account and its locations). Assumption: 10 residents per machine for sizing. The cost document explains what changes if customers means end users.

## Documents
Use cases in `usecases/`, user stories in `user-stories.md`, architecture in `architecture/`, message and REST contracts in `api/`, cost in `cost/`.
