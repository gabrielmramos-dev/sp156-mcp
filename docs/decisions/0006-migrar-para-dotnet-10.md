---
status: accepted
date: 2026-11-12
---

# Migrar o projeto de .NET 8 para .NET 10

## Contexto e problema

O projeto usa .NET 8 (`net8.0`, SDK 8.0.131). Segundo a [política de suporte do .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) (página atualizada em 10/11/2026), o .NET 8 saiu de suporte em 10/11/2026. Continuar nele significa não receber mais correções de segurança. Precisamos escolher a versão-alvo antes da gravação em SQLite (tarefa 8), que adiciona um pacote novo e não deve nascer em cima de um runtime sem suporte.

## Fatores de decisão

* Suporte ativo pelo maior tempo possível.
* Não migrar de novo logo depois.
* Mudança pequena: o projeto é uma biblioteca e testes, sem API de plataforma específica.

## Opções consideradas

* Ficar no .NET 8 (`net8.0`)
* Migrar para o .NET 10 (`net10.0`)
* Migrar para o .NET 11 (`net11.0`)

## Resultado da decisão

Escolhido o **.NET 10** (`net10.0`). Ele é LTS, foi lançado em 11/11/2025 e tem suporte até **14/11/2028**, segundo a página acima (data conferida em 12/11/2026). O SDK fixado em `global.json` é o 10.0.115, com `rollForward: latestFeature` como já era usado (só avança de feature band dentro da mesma versão principal).

Alternativas descartadas:

* **.NET 8:** sem suporte desde 10/11/2026.
* **.NET 11:** é STS e foi lançado em 10/11/2026, dois dias antes desta decisão. A mesma página dá o fim de suporte em **14/11/2028**, o mesmo do .NET 10, então não ganha tempo de suporte. Também não há SDK 11 instalado, e as bibliotecas que usamos (Sylvan.Data.Excel 0.x, xUnit) não foram testadas nele.

### Consequências

* Bom: runtime com suporte até 14/11/2028, sem nova migração obrigatória nesse período.
* Bom: o `Microsoft.Data.Sqlite` 10.x (previsto para a tarefa 8) e o .NET 10 acompanham a mesma versão principal.
* Ruim: o CI e quem contribui precisam do SDK 10.
* Ruim: a decisão vence em 14/11/2028. Quando o .NET 12 (LTS) sair, reavaliar.

### Confirmação

`dotnet test -c Release` com os mesmos totais de antes (sem e com `SP156_XLSX`) e nenhum pacote atualizado só por causa da migração.
