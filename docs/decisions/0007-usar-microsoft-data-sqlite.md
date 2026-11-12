---
status: accepted
date: 2026-11-12
---

# Gravar em SQLite com Microsoft.Data.Sqlite 10.0.15

## Contexto e problema

O Marco 1 grava os `Pedido` em um arquivo SQLite (spec M1). Precisamos de um acesso ADO.NET a SQLite, com uma dependência só.

## Opções consideradas

* Microsoft.Data.Sqlite, na série 10.x (alinhada ao .NET 10, [ADR 0006](0006-migrar-para-dotnet-10.md))
* Microsoft.Data.Sqlite 11.0.0
* Outros acessos a SQLite (não comparados nesta ADR: a escolha do pacote foi decisão do autor do projeto)

## Resultado da decisão

Escolhido **Microsoft.Data.Sqlite 10.0.15** (publicada em 10/11/2026), fixada em `src/Sp156.Ingest`. A 11.0.0 saiu no mesmo dia e a API do NuGet lista para ela só o alvo `net10.0`; a série 10.x lista `netstandard2.0`. Ficamos na 10.x, a mesma série do .NET 10 escolhido no ADR 0006. Nenhum outro pacote foi adicionado.

Verificado em 12/11/2026:

* **Licença:** MIT (`Microsoft.Data.Sqlite` e `Microsoft.Data.Sqlite.Core`, conforme o `nuspec`). Os repositórios `dotnet/efcore` e `dotnet/dotnet`, este último o indicado no `nuspec`, também são MIT.
* **Dependências transitivas:** `Microsoft.Data.Sqlite.Core` 10.0.15 e `SQLitePCLRaw` 2.1.13 (`bundle_e_sqlite3`, `core`, `provider.e_sqlite3` e `lib.e_sqlite3`), todos Apache-2.0 (autor Eric Sink, repositório `ericsink/SQLitePCL.raw`, Apache-2.0).
* **Nativo:** o pacote `SQLitePCLRaw.lib.e_sqlite3` traz o SQLite compilado (`libe_sqlite3.so`, `.dylib`, `e_sqlite3.dll`) para dezenas de plataformas; o `libe_sqlite3.so` de `linux-x64` tem 1,5 MB. O SQLite embutido é o **3.53.3** (`select sqlite_version()` em um projeto de teste .NET 10 descartável). No projeto de testes, a saída do build ganhou uma pasta `runtimes` com 22 plataformas, 33 MB.
* **Segurança:** `dotnet list package --vulnerable --include-transitive` não acusou pacote vulnerável, e o restore não emitiu avisos `NU19xx`.

### Consequências

* Bom: pacote da Microsoft, MIT, sem acesso a rede e sem servidor.
* Bom: sem vulnerabilidade conhecida pelo NuGet audit na data.
* Ruim: o SQLite vem em binário nativo de terceiros (Apache-2.0), então as atualizações de segurança do SQLite chegam por nova versão do `SQLitePCLRaw`, não pelo sistema.
* Ruim: quem consome a biblioteca recebe nativos de várias plataformas na saída do build. Se isso pesar, restringir por `RuntimeIdentifier` numa tarefa futura.
* Ruim: a 10.0.15 não é a mais recente (a 11.0.0 existe). Reavaliar quando migrarmos de .NET.

### Confirmação

Testes de gravação (tarefa 8) e o NuGet audit no restore.
