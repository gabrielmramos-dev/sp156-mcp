---
status: accepted
date: 2026-10-23
---

# Registrar decisões de arquitetura com MADR

## Contexto e problema

O projeto toma decisões (privacidade, escopo, bibliotecas) que precisam ser lembradas e questionadas depois, inclusive por quem não esteve na conversa.

## Opções consideradas

* Não registrar nada
* Comentários em issues
* ADRs em Markdown (MADR) dentro do repositório

## Resultado da decisão

Escolhida a terceira: ADRs em `docs/decisions/NNNN-titulo-com-hifens.md`, no formato MADR. Ficam versionados junto ao código e revisáveis em PR.

### Consequências

* Bom: decisões e alternativas descartadas ficam rastreáveis.
* Ruim: exige disciplina para escrever. Só decisões relevantes ganham ADR.
