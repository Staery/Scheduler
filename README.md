<div align="center">

<img src="docs/logo.png" width="96" alt="Scheduler logo" />

# Scheduler

**A virtualized WPF timeline widget that scrolls and zooms smoothly through a million scheduled jobs.**

[![CI](https://github.com/Staery/Scheduler/actions/workflows/ci.yml/badge.svg)](https://github.com/Staery/Scheduler/actions/workflows/ci.yml)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF-0C54C2?logo=windows&logoColor=white)
![C# 12](https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white)
![MVVM](https://img.shields.io/badge/pattern-MVVM-6366F1)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**English** · [Русский](README.ru.md)

</div>

---

Scheduler shows jobs (*Pending*, *Jeopardy*, *Completed*) on a multi-row timeline with a ruler, a live **NOW** line
and hover details. Its custom `TimelineView` control **draws only what is on screen** directly into a
`DrawingContext`, and finds the visible jobs with a binary search. The view stays responsive with 1,000,000 events:
only a few hundred are drawn per frame.

## 📸 Screenshots

![Timeline with the NOW line and a hovered job](docs/screenshots/timeline.png)

*One million events in 100 layers: generated in under a second, and each frame draws only the visible ones:*

![One million events](docs/screenshots/million-events.png)

## ✨ Features

| | |
|---|---|
| ⚡ **Virtualized rendering** | No UI element per event. Visible jobs are drawn into a `DrawingContext` with frozen brushes, and the time per frame is shown in the status bar |
| 🔎 **O(log n) visibility queries** | Each layer is a sorted, non-overlapping array, so the jobs in the viewport are found with a binary search |
| 🖱 **Navigation** | Wheel scrolls rows, `Shift`+wheel scrolls time, and `Ctrl`+wheel zooms around the cursor. There is a zoom slider, scroll bars, and a *Now* button that jumps to the NOW line |
| ⏱ **Live NOW line** | Play/pause with adjustable speed (1–240 minutes per second) |
| 🎛 **Status filters** | Show or hide Pending, Jeopardy and Completed jobs, with counts |
| 🧭 **Smart ruler** | Tick spacing adapts to the zoom (5 minutes to a week), and day boundaries are highlighted |
| 💬 **Hover details** | The job under the cursor is outlined, and its title, layer, time and status are shown |
| 🎲 **Realistic generator** | 1–200 layers and up to 1,000,000 jobs of 15 minutes to 3 hours. Past jobs are mostly completed, future ones pending. Generation runs off the UI thread |

## 🧱 Tech stack

| Area | Technology |
|---|---|
| Runtime | .NET 8, C# 12 |
| UI | WPF: custom `FrameworkElement` with `OnRender`, dependency properties, a custom theme and vector icons |
| Architecture | MVVM with [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/); `TimeProvider` for testable time |
| Tests | xUnit, 43 tests, including a check of the binary-search query against a brute-force scan |
| CI/CD | GitHub Actions: build, test and a self-contained single-file `.exe` |

## 🏗 Architecture

```mermaid
flowchart LR
    subgraph App["Scheduler (WPF, net8.0-windows)"]
        V[MainWindow.xaml]
        T[TimelineView<br/>OnRender · hit-testing · zoom]
    end

    subgraph Core["Scheduler.Core (net8.0)"]
        VM[MainViewModel]
        G[ScheduleGenerator]
        S[Schedule<br/>sorted layers · binary search]
        A[TimeAxis · OrdinalDate]
    end

    V -- binding --> VM
    T -- Schedule, zoom, filters --> S
    T --> A
    VM --> G --> S
```

**How a frame is drawn**

1. The viewport is converted to a time window `[from, to)` and a range of visible layers.
2. For each visible layer, `Schedule.Query(layer, from, to)` binary-searches the first job that ends after `from`,
   then walks forward until a job starts after `to`.
3. Only those jobs are drawn, together with the grid, the ruler, the sticky layer labels and the NOW line.

### Project layout

```
Scheduler/
├── src/
│   ├── Scheduler/               # WPF app: TimelineView control, window, theme
│   └── Scheduler.Core/          # Schedule, generator, time axis, view model
├── tests/Scheduler.Core.Tests/  # xUnit tests
└── .github/workflows/ci.yml
```

## 🛠 Fixes compared to the first version

- **Events were lost.** The AVL tree from Bitlush rejects duplicate keys, so every event whose start time collided
  with another one was silently dropped, while the Pending/Jeopardy/Completed counters still included it.
- **Memory leak.** The ruler added new elements on every scroll and never removed the old ones.
- **Debug code in the release build.** `AllocConsole()` opened a console window, and FPS was written to it on every
  scroll.
- **Threading.** Generation on a background thread replaced the tree the UI thread was reading. Now an immutable
  schedule is built off the UI thread and swapped in.
- **Edge cases.** Zero layers or a small number of events caused a division by zero, and one extra event was created
  per layer.
- The NOW line moved by a fixed 0.4 px every 10 ms, regardless of zoom. It now moves in timeline minutes.

## 🚀 Getting started

Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/Staery/Scheduler.git
cd Scheduler
dotnet run --project src/Scheduler
```

```bash
dotnet test tests/Scheduler.Core.Tests   # runs on any OS
```

| Input | Action |
|---|---|
| Wheel / `Shift`+wheel | Scroll layers / time |
| `Ctrl`+wheel | Zoom around the cursor |
| `Ctrl+G` | Generate a new schedule |
| `Ctrl+Space` | Play / pause the NOW line |

## 📄 License

[MIT](LICENSE) © 2024–2026 Anton Selkin
