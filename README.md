# NovaTech RT-500 Trajectory Postprocessor

## Overview

This repository contains a **C#/.NET console application** that converts generic robot trajectory data (JSON)
into **executable NovaTech RT-500 robot programs**.

This solution was created for the **DevRob technical assessment** and mirrors real postprocessor work:
parsing unstructured documentation, handling ambiguous specifications, applying safe defaults,
and clearly communicating design decisions.

The emphasis of this solution is on **reasoning, validation, and communication**, not robotics background or complex math.

---

## Problem Context

DevRob’s platform generates **vendor-agnostic motion trajectories** that must be translated into
**robot-vendor-specific code**.

For the NovaTech RT-500 robot, the postprocessor must:

- Convert linear and joint motions into valid robot commands
- Handle **firmware-specific syntax differences**
- Apply documented defaults when values are missing
- Validate unsafe or invalid input
- Make assumptions explicit when documentation is ambiguous

Because robot programs control physical hardware, **safety and correctness** are critical.

---

## Architecture Scope Decision

This assessment could be implemented with a much simpler design.

I intentionally used a **slightly structured layout** to clearly separate:
- core validation rules
- trajectory processing logic
- input/output concerns

The goal is to make assumptions and safety rules easy to understand and review.
In a real production setting, the architecture would be **simplified or expanded**
based on scope, team size, and operational requirements.

---

## High-Level Structure

```
src/
└─ NovaTechPostProcessor
├─ Domain // Core rules & validation
├─ Application // Trajectory processing workflow
├─ Infrastructure // File I/O and logging
tests/
└─ NovaTechPostProcessor.Tests
```

### Design Principles
- Strong typing to avoid invalid states
- Fail-fast validation for unsafe inputs
- Clear separation of responsibilities
- Readable and interview-defensible code

---

## Robot Terminology (Quick Explanation)

- **Postprocessor**  
  A translation layer that converts generic trajectory data into robot-specific executable commands.

- **Firmware**  
  The robot’s operating system. Different firmware versions require different command syntax.

- **MOVL (Linear Motion)**  
  Moves the robot tool in a straight line in Cartesian space.

- **MOVJ (Joint Motion)**  
  Moves robot joints independently to reach a pose; path is not guaranteed to be linear.

- **Joint 6 Extended Range**  
  Joint 6 (wrist rotation) supports ±720°, unlike other joints.

---

## Extracted Robot Specification

Based on the provided documentation:

### Commands
- Linear: `MOVL P[x,y,z,rx,ry,rz] SPD=v ACC=a`
- Joint: `MOVJ J[j1,j2,j3,j4,j5,j6] SPD=v%`

### Units & Defaults
- Position: millimeters
- Angles: degrees
- Linear speed: mm/sec
- Joint speed: percentage
- Acceleration default: **50** if omitted

### Firmware Rules
- Firmware `< 3.1` → `SPD(v)`
- Firmware `>= 3.1` → `SPD=v`

---

## Handling Ambiguity

The documentation contains incomplete or unclear information.
Instead of guessing silently, the solution follows this approach:

1. Identify ambiguous behavior
2. Make a conservative, documented assumption
3. Apply validation where safety is involved
4. Add tests to lock in the interpretation
5. List open questions for clarification

### Example: Firmware Boundary

Documentation states:
> "Firmware <3.1 uses legacy speed syntax"

Interpretation:
- `< 3.1` → legacy syntax
- `>= 3.1` → modern syntax

This decision is explicitly documented and tested.

---

## Edge Case & Safety Handling

Because robot code controls physical hardware, unsafe input is rejected early.

### Validations
- Speed must be positive
- Default acceleration applied when missing
- Joint 6 validated against ±720°
- Coordinate arrays validated for correct length
- Missing optional fields handled consistently

### Example

```
Input: "speed": -50
Error: "Speed must be positive for safety reasons"
```


Fail-fast behavior prevents generation of unsafe robot programs.

---

## Generated Output (Example)

```
BASE P[0,0,0,0,0,0]
TOOL P[0,0,150,0,0,0]

MOVL P[500,200,300,0,90,0] SPD=100 ACC=75
MOVJ J[45,-30,60,0,45,180] SPD=50% ACC=50
```

Outputs are generated for both:
- `sample_trajectory.json`
- `edge_cases_trajectory.json`

---

## Testing Approach

Tests focus on **behavior**, not implementation details:

- Firmware-specific syntax switching
- Default value handling
- Boundary conditions (e.g., ±720°)
- Invalid input rejection
- Output correctness

Tests also act as **executable documentation** for assumptions.

---

## Assumptions

- Acceleration percentage refers to robot’s maximum capability
- Joint 6 supports symmetric ±720° range
- Mixed linear and joint motions are allowed
- Invalid input should fail fast rather than be auto-corrected

---

## Clarification Questions for Manufacturer

1. What are the official minimum and maximum speed limits?
2. Do joints 1–5 strictly enforce ±180° limits?
3. How should invalid trajectory points be handled in production?
4. Are multiple BASE/TOOL commands allowed in one program?
5. What numeric precision is officially supported?

---

## How to Run

```bash
git clone <repo-url>
cd devrob-assesment
dotnet build
dotnet run
dotnet test


```
Summary

This solution demonstrates:

Clear reasoning under ambiguous requirements

Safety-first validation

Firmware-aware behavior

Explicit communication of assumptions

The intent is not perfection, but to show how I think and reason
when working with incomplete specifications—exactly what the DevRob
assessment emphasizes.

```