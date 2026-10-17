# 🧩 Cilada 3D (Working Title) - Procedural Topology Puzzle

![Godot Engine](https://img.shields.io/badge/Godot_4.x-Mono%20%28C%23%29-blue?logo=godotengine)
![License](https://img.shields.io/badge/License-MIT-green)
![Status](https://img.shields.io/badge/Status-MVP-orange)

Uma recriação tridimensional e procedural inspirada no clássico jogo de tabuleiro "Cilada" (Estrela). Este projeto utiliza a **Godot Engine (C#)** para gerar desafios espaciais e topológicos onde o jogador precisa rotacionar e encaixar blocos com conectores complementares (Macho/Fêmea) para reconstruir uma forma tridimensional gerada aleatoriamente.

## ✨ Características do MVP

* **Geração Procedural (Spanning Tree):** Níveis infinitos gerados em tempo real. O algoritmo cria uma árvore de conexões em um grid 3D e extrai as peças necessárias, garantindo que todo puzzle tenha uma solução matemática perfeita.
* **Sistema Chave-Fechadura:** Conectores geométricos classificados (QuadExt/Int, CircExt/Int, CruzExt/Int) com validação rígida de encaixe espacial.
* **Interface "Blueprint" (Planta Baixa):** Dois minimapas interativos (estilo laboratório/Resident Evil) renderizados dinamicamente via projeção ortogonal a partir da topologia 3D, com suporte a Pan e Zoom acoplados ao cursor do mouse.
* **Snapping Inteligente:** Sistema de atração magnética com *Ghost Cube* preditivo que valida adjacências antes de permitir a junção das peças.
* **Arquitetura C# Modular:** Código escalável dividido em *Partial Classes* (`Logic`, `UI`, `Generator`, `Input`).

## 🧠 Fundamentação Teórica e Desafios Atuais

O núcleo lógico do jogo baseia-se na Teoria dos Grafos. Para comprovar que um conjunto de peças gerado aleatoriamente pode formar a topologia alvo:
1. **Modelagem:** O volume alvo é mapeado como um Grafo $G = (V, E)$, onde $V$ são os espaços cúbicos (nós) e $E$ são as adjacências (arestas).
2. **Caminho Euleriano/Hamiltoniano Espacial:** A geração garante a criação de uma *Spanning Tree* (Árvore Geradora) dentro da topologia, assegurando que não existam ilhas de nós desconectados.
3. **Mapeamento Topológico em Tempo Real (Desafio Atual):** O próximo passo é otimizar a comparação entre a assinatura espacial (matriz 3D) do subgrafo construído pelo jogador e o grafo alvo, validando não apenas a posição, mas a conectividade das faces em qualquer rotação no espaço $SO(3)$.

## 🚀 Como Executar

1. Clone o repositório: `git clone https://github.com/SamuelSBJr97/SSBJR.GameChallenges001.git`
2. Abra o projeto na **Godot 4.x (versão .NET/Mono)**.
3. Certifique-se de compilar o projeto C# (Build) antes de executar a cena principal (`Main.tscn`).
4. *Controles:* Botão Direito (Câmera), Roda do Mouse (Zoom), Clique Esquerdo (Selecionar/Arrastar/Pan no Minimapa), Setas/PgUp/PgDn (Rotação da Peça).

## 🤝 Contribuições e Parcerias

Este projeto está ativamente buscando colaboração! Se você é um acadêmico interessado em Teoria dos Grafos/Geração Procedural, um Game Designer, ou um estúdio buscando explorar quebra-cabeças mecânicos 3D, sinta-se à vontade para abrir uma *Issue* ou enviar um *Pull Request*.