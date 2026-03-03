# Easy Command – Community Edition

© 2019–2026 Starlight IT Solutions  
Designed and programmed by Anthony W. Marinello  

---
# Disclaimer
⚠️ This software drafts system commands for review. 
The user is fully responsible for reviewing and approving any command before execution.

This software is provided "AS IS" under the MIT License with no warranty or liability.

## Overview

Easy Command is a Windows WPF utility that converts natural language into drafted PowerShell or CMD commands, requires explicit user review, and executes only after approval.

**Workflow:**

Natural language → Drafted Windows command → User review → Approved execution

This Community Edition requires users to supply their own OpenAI API key.

---

## Safety Model

- Commands are never auto-executed.
- All drafts are displayed for review before execution.
- Potentially destructive commands require additional confirmation.
- Execution occurs locally on the user’s system.
- Users remain fully responsible for reviewing all generated scripts.

---

## API Configuration

On first launch, the application prompts for your OpenAI API key and stores it as a per-user environment variable (`OPENAI_API_KEY`).

No API keys are bundled or committed with this project.

Users are responsible for managing their own OpenAI API usage and billing.

---

## Build Instructions

Navigate to the project directory containing the `.csproj` file and run:

```powershell
dotnet restore
dotnet build -c Release


## Interface

---

![Main Window](docs/images/main-window.png)