# 🧠 InterviewSimulator

**AI-Powered Interview Practice Platform** built with **ASP.NET Web API**, **HTML/CSS/JavaScript**, **Gemini API**, and **Google Cloud Run**.

![.NET](https://img.shields.io/badge/.NET%208-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Google Cloud Run](https://img.shields.io/badge/Google%20Cloud%20Run-4285F4?style=for-the-badge&logo=googlecloud&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![Gemini API](https://img.shields.io/badge/Gemini%20API-FFD700?style=for-the-badge&logo=google&logoColor=black)

---

## 🌍 Overview

**InterviewSimulator** is a scalable AI web app that lets users **practice real-time job interviews** with an intelligent virtual interviewer.  
It uses **Google Gemini API** to generate dynamic interview questions, analyze responses, and provide instant AI-driven feedback.

The app is containerized with **Docker** and deployed to **Google Cloud Run** for seamless scalability and reliability.

---

## ⚙️ Tech Stack

| Layer | Technology |
|-------|-------------|
| **Frontend** | Blazor Web App (.NET 8) |
| **Backend** | ASP.NET Core + SignalR |
| **AI Engine** | Google Gemini API |
| **Architecture** | Clean Architecture (Domain, Application, Infrastructure, UI) |
| **Deployment** | Docker + Google Cloud Run |
| **Database (optional)** | PostgreSQL |

---

## 🚀 Features

✅ Real-time AI interview simulation  
✅ Smart question generation based on job roles  
✅ Instant feedback and response evaluation  
✅ Scalable and serverless deployment on Cloud Run  
✅ Clean architecture for maintainability and testability  

---

## 🧩 System Architecture

User (Browser) → Frontend (HTML/CSS/JS) → ASP.NET Web API → SignalR Hub → Gemini API → Response → User


- The user interacts through the **HTML/CSS/JS frontend**.  
- Requests are sent to **ASP.NET Web API**, which handles logic and communicates with **SignalR Hub** for real-time updates.  
- **Gemini API** generates AI interview questions and evaluates answers.  
- Responses are sent back to the user instantly.  
- The whole system is **containerized with Docker** and deployed on **Google Cloud Run** for scalability and reliability.

## 🛠️ Run locally (PowerShell)

Install the .NET 8 SDK (or a newer SDK with the .NET 8 ASP.NET Core runtime), then run these commands from the repository root. The first build needs NuGet access.

```powershell
$env:Gemini__ApiKey = "<your Gemini API key>"
$env:DOTNET_ENVIRONMENT = "Production"
dotnet run --project InterviewSimulator.Api --no-launch-profile --artifacts-path ../InterviewSimulator-artifacts
```

Replace the key placeholder locally; do not commit a real key to `appsettings.json`. `Gemini__ApiKey` overrides the nested `Gemini:ApiKey` setting. The default model and endpoint come from `InterviewSimulator.Api/appsettings.json`. The standalone `GeminiApiKey` setting is not used by the service.

Open **http://localhost:8080/** for the interview page. **http://localhost:8080/api-status** should return `AI Interview Simulator API is running.` without calling Gemini. Select a role and level, click **Start Interview**, then submit an answer. Generating questions and feedback requires a valid key, access to the configured model, and internet access; loading the page alone does not validate the key.

`Program.cs` binds to `http://0.0.0.0:8080`, including all network interfaces, even though the launch profiles list other ports. Keep the server on a trusted local network and stop it with **Ctrl+C** when finished. The command skips those launch profiles and uses `Production` to avoid the development HTTPS redirect; Swagger is only enabled in `Development`. Build outputs go outside the checkout to `../InterviewSimulator-artifacts`.

No database is configured in the current startup code; PostgreSQL and Cloud Run are not prerequisites for this local flow.

See Microsoft's references for [environment-variable configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0#environment-variables) and [`dotnet run` options](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-run).
  
