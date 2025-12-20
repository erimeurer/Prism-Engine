# Prism Engine

**Prism Engine** is a game engine under active development, built in **C#**, focused on **full architectural control**, **predictable performance**, and **custom editor tooling**.

The project is inspired by modern engines such as Unity, Stride, and Unreal, but aims to avoid black-box systems and hidden behaviors, giving developers full visibility and control over how the engine works.

---

## ✨ Project Goals

- Fully **code-driven** engine architecture
- Custom visual editor (Unity-like workflow)
- Modular and extensible systems
- Modern rendering pipeline
- Foundation for **large worlds / scene streaming**
- Deep focus on learning and engine development

---

## 🧠 Project Philosophy

> Prism Engine is not trying to be a generic solution for every use case.  
> It is an **opinionated engine**, focused on control, clarity, and performance.

- No magic systems
- No heavy third-party dependencies
- Everything is inspectable and extensible
- Designed for developers who want to understand how a game engine works internally

---

## 🧩 Architecture Overview

- **GameObject + Component** system
- Rendering built on top of **MonoGame**
- Clear separation between:
  - Engine Core
  - Editor
  - Runtime
- Decoupled systems (rendering, input, animation, scene management)

---

## 🎮 Current Features

- ✔️ Custom visual editor
- ✔️ Scene system
- ✔️ GameObjects and Components
- ✔️ Model rendering
- ✔️ Skeletal animation (Skinned Mesh)
- ✔️ Editor gizmos
- ✔️ Asset browser
- ✔️ Evolving render pipeline

---

## 🌍 Large Worlds & Open Scenes (Work in Progress)

Prism Engine is being designed to support:

- Grid-based scene streaming
- Automatic activation/deactivation by distance
- World partition–like cell division
- Foundation for LOD, impostors, and large-scale optimizations

> The goal is full control over world streaming, not opaque automation.

---

## 🛠️ Technology Stack

- **Language:** C#
- **Graphics backend:** MonoGame
- **Editor:** Custom editor (WPF-based tooling)
- **Target platform:** Windows (for now)

---

## 🚧 Project Status

⚠️ **Actively developed project**

- APIs are subject to change
- Not production-ready yet
- Best suited for learning, experimentation, and long-term evolution

---

## 📌 Roadmap (High-Level)

- [ ] Advanced material system
- [ ] Improved HDR / PBR lighting
- [ ] Automatic world streaming
- [ ] Dedicated physics system
- [ ] Editor UX and performance improvements
- [ ] Technical documentation

---

## 🤝 Contributions

The project is currently developed independently.  
Ideas, discussions, and suggestions are welcome via issues.

---

## 📄 License

License to be defined.

---

## 📷 Preview

![Scene Editor](docs/images/first.png)
![Editor Gizmos](docs/images/gizmos.png)
![Skinned Mesh](docs/images/skinned_mesh.png)
