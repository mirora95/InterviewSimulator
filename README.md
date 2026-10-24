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
| **Frontend** | HTML/CSS/JavaScript served from `InterviewSimulator.Api/wwwroot` |
| **Backend** | ASP.NET Core + SignalR |
| **AI Engine** | Google Gemini API |
| **Architecture** | Clean Architecture (Domain, Application, Infrastructure, UI) |
| **Deployment** | Docker + Google Cloud Run |
| **Database (optional)** | PostgreSQL |

---

## Regression tests

Run these commands from the repository root with Node.js 24 and the .NET 8 SDK installed. A newer .NET SDK can also build the tests, but the .NET 8 runtime is still needed to run them.

```sh
node --test InterviewSimulator.Tests/interview.test.cjs
dotnet test InterviewSimulator.sln --configuration Release --artifacts-path .artifacts
```

The client tests use Node's built-in test runner and execute the real browser script with mocked DOM and SignalR APIs; no npm install is needed. The .NET tests build the solution and use in-process test services and fake Gemini responses. They do not require an API key, PostgreSQL, or a Cloud Run account. The first .NET package restore requires access to NuGet.

Build output is isolated in `.artifacts/` so the test command does not rewrite checked-in `bin/` and `obj/` files. The `Regression tests` workflow in `.github/workflows/tests.yml` runs both suites for pull requests and pushes to `main`.

These checks do not exercise a live browser, a deployed application, or real Gemini requests.

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
  
