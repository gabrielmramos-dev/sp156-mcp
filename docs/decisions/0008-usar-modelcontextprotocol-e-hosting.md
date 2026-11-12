---
status: accepted
date: 2026-11-12
---

# Servidor MCP com ModelContextProtocol 2.4.0 e Microsoft.Extensions.Hosting 10.0.15

## Contexto e problema

O Marco 2 expõe consultas sobre o `.db` por um servidor MCP sobre stdio ([spec](../spec-m2-servidor-mcp.md)). Precisamos de um SDK que fale o protocolo e de um host que cuide de configuração, injeção de dependência e logs.

## Opções consideradas

* `ModelContextProtocol` 2.4.0 + `Microsoft.Extensions.Hosting` 10.0.15
* `ModelContextProtocol.Core` 2.4.0 sozinho (a documentação do SDK o indica para quem quer o mínimo de dependências, só com as APIs de cliente e de servidor de baixo nível, sem hosting nem descoberta de ferramentas por atributos)
* `Microsoft.Extensions.Hosting` 11.0.0 em vez da 10.0.15
* Outras bibliotecas de MCP para C# (não comparadas: o pacote é o que a documentação oficial do SDK indica, e a escolha foi do autor do projeto)

## Resultado da decisão

Escolhidos `ModelContextProtocol` **2.4.0** (publicado em 07/11/2026) e `Microsoft.Extensions.Hosting` **10.0.15** (publicado em 10/11/2026), em `src/Sp156.Mcp`. A documentação do SDK indica o pacote `ModelContextProtocol` como o ponto de partida para um servidor **stdio** (traz hosting, injeção de dependência e descoberta de ferramentas por atributos), e ele referencia o `ModelContextProtocol.Core`. O Hosting fica na **série 10.0.x**, a mesma do .NET 10 ([ADR 0006](0006-migrar-para-dotnet-10.md)) e do `Microsoft.Data.Sqlite` ([ADR 0007](0007-usar-microsoft-data-sqlite.md)); a 11.0.0 saiu no mesmo dia da 10.0.15 e **não** é usada. Nenhum outro pacote foi adicionado.

Verificado em 12/11/2026:

* **Licenças (nuspec):** dos 32 pacotes novos, 30 são **MIT** (todos da Microsoft, a família `Microsoft.Extensions.*` e `System.Diagnostics.EventLog`) e 2 são **Apache-2.0** (`ModelContextProtocol` e `ModelContextProtocol.Core`).
* **A licença do SDK é de transição:** o `LICENSE` do repositório `modelcontextprotocol/csharp-sdk` diz que o projeto está passando da licença MIT para a Apache-2.0, que o código novo é Apache-2.0 e que contribuições de autores que ainda não consentiram a mudança continuam MIT; o arquivo traz os dois textos. O GitHub não classifica o arquivo (`NOASSERTION`), e o `nuspec` declara só Apache-2.0. As duas licenças são permissivas; isto não é parecer jurídico.
* **Dependências novas** (além das já existentes do Marco 1): `ModelContextProtocol.Core` 2.4.0, `Microsoft.Extensions.AI.Abstractions` **10.11.0**, `Microsoft.Extensions.Caching.Abstractions` 10.0.14, `Microsoft.Extensions.Logging.Abstractions` 10.0.15 e a família `Microsoft.Extensions.*` 10.0.15 (Configuration, DependencyInjection, Diagnostics, FileProviders, Hosting, Logging com os provedores Console, Debug, EventLog e EventSource, Options e Primitives), mais `System.Diagnostics.EventLog` 10.0.15.
* **Nativo:** nenhum componente nativo novo. O único binário nativo do projeto continua sendo o SQLite do [ADR 0007](0007-usar-microsoft-data-sqlite.md). O `System.Diagnostics.EventLog` traz só uma pasta `runtimes/win/lib` (código gerenciado, para o log de eventos do Windows).
* **Segurança:** `dotnet list package --vulnerable --include-transitive` diz que o projeto não tem pacote vulnerável, e o restore não emitiu avisos `NU19xx`.

### Consequências

* Bom: SDK oficial, com hosting e injeção de dependência prontos e transporte stdio e em memória (útil para testes).
* Bom: licenças permissivas e sem vulnerabilidade conhecida pelo NuGet audit na data.
* Ruim: são 32 pacotes novos (a maioria `Microsoft.Extensions.*` que o Hosting já traz), e uma dependência de versão diferente da série (`Microsoft.Extensions.AI.Abstractions` 10.11.0, imposta pelo SDK).
* Ruim: a licença do SDK está em transição (Apache-2.0 e MIT). Reavaliar se o texto do `LICENSE` mudar.
* Ruim: o SDK é novo (2.4.0 de 07/11/2026); o comportamento só é confirmado por testes (ver as pendências da spec).

### Confirmação

Testes do servidor (spec do Marco 2, tarefa M2-1 em diante) e o NuGet audit no restore.
