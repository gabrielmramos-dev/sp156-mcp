# Spec M2: Servidor MCP do SP156

> **Status: rascunho de especificação (tarefa M2-0), escrito em 2026-11-12 e ajustado no mesmo dia com as decisões do autor (seção 3, decisões 10 a 14). M2-1 (esqueleto do servidor), M2-2 (catálogo e validação de entrada) e M2-3 (`descrever_dataset`) implementadas; decisões 15 a 18 tomadas na M2-3. O que a implementação confirmou está na §4 e na §15.**

Convenções deste documento:

- **Decisão:** já tomada pelo autor do projeto. Não reabrir sem avisar.
- **Proposta (a confirmar):** sugerida nesta spec, ainda sem decisão.
- **A verificar no código:** a documentação do SDK consultada não confirma. Nada disso é afirmado aqui; cada item vira um teste ou uma leitura do código na tarefa indicada.

## 1. Objetivo

Expor, por MCP sobre stdio, consultas **agregadas** (contagens e estatísticas) sobre o banco SQLite gerado no Marco 1, sempre acompanhadas das limitações dos dados. O servidor nunca devolve linhas individuais.

## 2. Escopo e fora de escopo

**Dentro do escopo**

- Servidor MCP sobre **stdio**, em `src/Sp156.Mcp` (console, `net10.0`).
- Pacotes `ModelContextProtocol` (2.4.0) e `Microsoft.Extensions.Hosting`, este fixado na série 10.0.x (**não** a 11.0.0). Nenhum outro pacote.
- Leitura **somente leitura** do `.db` do Marco 1.
- Três ferramentas: `descrever_dataset`, `contar_pedidos` e `tempo_ate_finalizar`.

**Fora de escopo (não tocar)**

- HTTP (Streamable HTTP e SSE) e autenticação.
- Prompts e resources do MCP.
- Outros trimestres (o `.db` cobre só o 1º trimestre de 2025).
- GeoSampa.
- Qualquer escrita: inclui criar índice ou alterar o esquema do `.db`.
- Ler o XLSX: o servidor nunca o abre.

## 3. Decisões já tomadas

| # | Decisão | Detalhada em |
|---|---|---|
| 1 | Escopo: servidor stdio em `src/Sp156.Mcp` (net10.0), pacotes `ModelContextProtocol` 2.4.0 e `Microsoft.Extensions.Hosting`. Fora: HTTP, autenticação, prompts, resources, outros trimestres, GeoSampa, escrita | §2 |
| 2 | O servidor só lê o `.db` (`Mode=ReadOnly`). Caminho por `--db` ou pela variável `SP156_DB`. Nunca toca o XLSX | §5 |
| 3 | Stdout é o canal do protocolo: todos os logs vão para stderr. Um teste com processo real prova que o stdout só tem mensagens do protocolo | §5, §12 |
| 4 | Três ferramentas, todas devolvendo, além do dado, o campo `limitacoes` (decisão 14 detalha os textos) | §6 a §9 |
| 5 | Filtros categóricos casam por `TextoNormalizado.Chave`. Na partida, o servidor carrega os valores de cada coluna categórica e monta o mapa Chave para valores originais; a consulta usa os valores originais exatos. **Só entram no mapa valores com 5 ou mais linhas** (decisão 13). Sem mudança de esquema | §6.3 |
| 6 | Valor de filtro desconhecido: erro com a lista dos valores válidos mais próximos, **só dentre os valores com 5 ou mais linhas** (permitido: são categorias do conjunto de dados, não dados pessoais). Nenhuma ferramenta devolve linhas individuais, só contagens e estatísticas | §10, §11 |
| 7 | Entrada inválida: `McpException` com mensagem clara e sem conteúdo de célula. Exceção inesperada: mensagem genérica do SDK. O comportamento exato é confirmado por teste | §10 |
| 8 | Testes em 3 camadas: transporte em memória, processo real stdio e opt-in com o arquivo real | §12 |
| 9 | Valores de referência calculados com pandas sobre o mesmo arquivo | §13 |
| 10 | `meio_comunicacao` entra como filtro e como `agrupar_por` (6 valores, 1 nulo). `servico` e `prefeitura_operacional` entram em `agrupar_por`; `prefeitura_operacional` também é filtro | §6, §8 |
| 11 | Campos extras aceitos: `sem_valor`, `total_de_grupos`, `linhas_sem_valor` e `n_canceladas` | §7 a §9 |
| 12 | Duração negativa: campo separado, fora das estatísticas. Média com 1 casa, arredondada para longe de zero. Filtro com string vazia ou só espaços: `McpException` clara (não ignorar) | §9, §10 |
| 13 | Supressão: em saídas **agrupadas**, grupos com menos de 5 pedidos viram uma linha "outros (agrupados)", preservando os totais. Valores com menos de 5 linhas ficam fora do mapa de Chave e das listas de categorias mais próximas nos erros. Totais exatos. **Não é garantia formal de anonimato** | §6.3, §8, §11 |
| 14 | `limitacoes`: textos fixos como constantes no código, com teste; números e datas calculados do banco na consulta. Sem tabela de metadados e sem mudança de esquema | §6.4 |
| 15 | `descrever_dataset` devolve `valores_no_catalogo` (decisão em aberto 3) | §7 |
| 16 | O catálogo e as medidas de `limitacoes` (inclusive as duplicatas) são calculados uma vez por processo e guardados em memória (decisão em aberto 4) | §5, §6.4 |
| 17 | Ordenação de `por_status` (pedidos decrescente, empate por `valor` crescente) e de `por_mes` (cronológica), como proposto na §7 | §7 |
| 18 | Percentual de duplicatas com 2 casas, arredondado para longe de zero, como a média (decisão 12) | §6.4 |

## 4. O que a documentação do SDK confirma

Fontes consultadas em 2026-11-12 (documentação v2 do SDK C#):
[getting-started](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/getting-started.html),
[tools](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/tools/tools.html) e
[transports](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html).

| Assunto | O que a documentação confirma | O que fica a verificar |
|---|---|---|
| Pacotes | `ModelContextProtocol` é o ponto de partida para um servidor **stdio** (traz hosting, injeção de dependência e descoberta por atributos) e referencia `ModelContextProtocol.Core`. Instalação: `dotnet add package ModelContextProtocol` e `dotnet add package Microsoft.Extensions.Hosting` | Versões resolvidas, licenças, nativo e NuGet audit: ADR 0008 (M2-1). O `Microsoft.Extensions.Hosting` fica na série 10.0.x, alinhada ao .NET 10 (ADR 0006). A versão 2.4.0 do `ModelContextProtocol` (publicada em 07/11/2026, alvos `net10.0`, `net8.0`, `net9.0` e `netstandard2.0`, licença Apache-2.0) foi conferida na API do NuGet, não nestas páginas |
| Servidor stdio | `Host.CreateApplicationBuilder(args)`, `builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` (ou `.WithTools<T>()`), `await builder.Build().RunAsync()` | **Confirmado na M2-1 (sonda e teste):** quando o stdin fecha, o servidor encerra sozinho com código `0` |
| Logs | O exemplo oficial usa `builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)` com o comentário "Configure all logs to go to stderr" | **Confirmado na M2-1:** com essa configuração o stdout fica com 0 bytes na partida e o log do host e do SDK (`info: ...`, `Application started...`) sai no stderr. Com o logger do console no padrão (mutação), o `info: ModelContextProtocol.Server.StdioServerTransport...` sai no **stdout** e corromperia o protocolo. A conferência com uma conversa de protocolo completa fica para a M2-6 |
| Ferramenta por atributos | `[McpServerToolType]` na classe, `[McpServerTool, Description("...")]` no método, `[Description]` nos parâmetros. Os parâmetros são desserializados do JSON, e o valor padrão (`int maxResults = 10`) aparece no esquema. Um `string` devolvido vira `TextContentBlock` | **Confirmado em sonda no SDK 2.4.0:** o nome publicado é o do método em `snake_case` (`DescreverDataset` vira `descrever_dataset`), e `[McpServerTool(Name = "...")]` também funciona. `string? tema = null` vira parâmetro opcional (`"type": ["string","null"], "default": null`); `int limite = 20` vira opcional com `"default": 20`; parâmetro sem valor padrão é obrigatório. **Tipo errado** no JSON (por exemplo `limite: "abc"`) dá `IsError = true` com o texto genérico `An error occurred invoking 'contar_pedidos'.` (não é erro JSON-RPC). **Parâmetro desconhecido é ignorado em silêncio**: a chamada roda como se ele não existisse (ver §15). Injeção por construtor na classe da ferramenta funciona com `WithTools<T>()` |
| Saída estruturada | `UseStructuredContent = true` no atributo (ou em `McpServerToolCreateOptions`) publica um esquema de saída e serializa o retorno em `StructuredContent`. Um valor que não é objeto não é embrulhado em `result` | **Confirmado em sonda:** `[JsonPropertyName]` é respeitado na saída e no esquema (`total_linhas`); propriedade sem o atributo sai em `camelCase` (`semAtributo`), então **toda** propriedade precisa do atributo. `UseStructuredContent = true` **também envia um bloco de texto** com o mesmo JSON. No cliente, `CallToolResult.StructuredContent` é um `JsonElement?`. Um `int?` vira `["integer","null"]`, e todas as propriedades entram em `required`, inclusive as anuláveis |
| Erros | Exceção lançada pela ferramenta vira `CallToolResult` com `IsError = true`. Se for `McpException` (exceto `McpProtocolException`), a mensagem entra no texto do erro; qualquer outra exceção vira uma mensagem genérica. `McpProtocolException` vira erro JSON-RPC (ex.: `-32602 InvalidParams`). `OperationCanceledException` é relançada se o token foi cancelado | **Confirmado em sonda e no código:** `McpException` tem os construtores `()`, `(string message)` e `(string message, Exception innerException)`; `McpProtocolException` deriva dela. O texto do erro é `An error occurred invoking '<nome>': <mensagem>` (um prefixo em inglês e a nossa mensagem em português depois); para qualquer outra exceção, só `An error occurred invoking '<nome>'.` (a mensagem da exceção **não** vaza). Ferramenta inexistente lança `McpProtocolException` no cliente (`Unknown tool: '<nome>'`, erro JSON-RPC) |
| Transporte em memória | `StreamServerTransport` sobre `Pipe`, `McpServer.Create(transport, new McpServerOptions { ToolCollection = [McpServerTool.Create(...)] })`, `server.RunAsync()`, e no cliente `McpClient.CreateAsync(new StreamClientTransport(...))`, `ListToolsAsync`, `CallToolAsync` | **Confirmado na M2-1:** o caminho para as nossas classes é um `Host` com injeção de dependência, `AddMcpServer().WithStreamServerTransport(Stream entrada, Stream saida)` e `.WithTools<T>()`, ligado a `McpClient.CreateAsync(new StreamClientTransport(...))` (`StreamClientTransport` está em `ModelContextProtocol.Protocol`). `McpServer.Create(ITransport, McpServerOptions, ILoggerFactory, IServiceProvider)` também existe, mas não foi necessário |
| Sem ferramentas | (não consta nas páginas) | **Corrige esta spec:** sem nenhuma ferramenta registrada, `ServerCapabilities.Tools` é `null` e `ListToolsAsync` lança `McpProtocolException` com `Method 'tools/list' is not available.`. A versão anterior da spec dizia que o servidor "lista zero ferramentas"; isso é falso. O teste do M2-1 afirma o comportamento real |
| Cliente stdio | `StdioClientTransport(new StdioClientTransportOptions { Command, Arguments, WorkingDirectory, EnvironmentVariables, InheritEnvironmentVariables (padrão `true`), ShutdownTimeout (padrão 5 s), StandardErrorLines, Name })` e `McpClient.CreateAsync(transport)` | Ainda não usado (M2-6) |
| Enquadramento no stdio | (vem da especificação do protocolo, não destas páginas) | **Observado numa sonda:** uma mensagem JSON por linha no stdout, todas com `"jsonrpc": "2.0"`. Um `initialize` com `protocolVersion` `2025-11-25` foi aceito, seguido de `notifications/initialized`, `tools/list` e `tools/call`, e o servidor respondeu 3 linhas (ids 1, 2 e 3). A M2-6 faz isso em teste |

## 5. Configuração e execução

```bash
dotnet run --project src/Sp156.Mcp -- --db <caminho.db>
SP156_DB=<caminho.db> dotnet run --project src/Sp156.Mcp
```

- **Decisão:** o servidor abre o `.db` com `Mode=ReadOnly` e nunca escreve nele nem abre o XLSX.
- **Decisão:** o stdout é só do protocolo. Todos os logs vão para o stderr (`LogToStandardErrorThreshold = LogLevel.Trace`, como no exemplo da documentação). O código do servidor não usa `Console.Out`, `Console.Write` nem `Console.WriteLine`.
- **Proposta (a confirmar):** `--db` tem precedência sobre `SP156_DB`. Sem nenhum dos dois, ou com um caminho que não existe, o servidor escreve uma mensagem no stderr e termina com código `1`, sem iniciar o protocolo.
- **Proposta (a confirmar):** na partida, o servidor confere que a tabela `pedidos` existe com as 10 colunas do esquema do Marco 1 ([spec M1](spec-m1-ingestao.md)) e, se não, termina com código `1` e uma mensagem clara.
- **Proposta (a confirmar):** uma conexão `Mode=ReadOnly;Pooling=False` por chamada de ferramenta, como no ADR 0007, sem estado compartilhado além do catálogo da §6.3, que é imutável.
- **Carga do catálogo (corrigido na M2-3):** o catálogo da §6.3 e as medidas da §6.4 são carregados na **primeira chamada** que precisar deles, não na partida (`DadosDoBanco`, com `Lazy<T>` thread-safe), e guardados até o fim do processo (decisão 16). Motivo: o servidor sobe e lista as ferramentas sem abrir o banco, e os testes que registram o servidor com um caminho fictício continuam válidos; falhar na partida com banco inválido é a decisão em aberto 6. Consequência: a primeira chamada paga a carga, e uma falha nela fica guardada pelo `Lazy` até reiniciar o processo. O catálogo usa um `GROUP BY ... HAVING count(*) >= 5` por coluna. Medida de ordem de grandeza (Python com SQLite 3.45.1, não o C#): 8 consultas `GROUP BY` levaram 2,5 s no `.db` do arquivo real. O `.db` não tem índices, e o servidor não pode criá-los (somente leitura).

## 6. Vocabulário comum das ferramentas

### 6.1 Colunas categóricas

`tema`, `assunto`, `servico`, `status`, `orgao`, `prefeitura_operacional`, `distrito` e `meio_comunicacao` (decisão 10). Medido no `.db` do arquivo real em 2026-11-12, contando valores não nulos:

| Coluna | Valores distintos | Com 5 ou mais linhas (entram no catálogo) | Com menos de 5 linhas (ficam fora) | Linhas sem valor |
|---|---|---|---|---|
| `tema` | 28 | 27 | 1 | 0 |
| `assunto` | 204 | 182 | 22 | 0 |
| `servico` | 707 | 563 | 144 | 0 |
| `status` | 4 | 4 | 0 | 0 |
| `orgao` | 517 | 430 | 87 | 0 |
| `prefeitura_operacional` | 38 | 36 | 2 | 367.648 |
| `distrito` | 96 | 96 | 0 | 374.346 |
| `meio_comunicacao` | 6 | 6 | 0 | 1 |

No total são 1.600 valores distintos, dos quais 1.344 têm 5 ou mais linhas e 256 têm menos.

### 6.2 Filtros

Todos opcionais, combinados com **E**.

| Parâmetro | Tipo | Regra |
|---|---|---|
| `tema`, `assunto`, `servico`, `status`, `orgao`, `prefeitura_operacional`, `distrito`, `meio_comunicacao` | texto | Casa por `TextoNormalizado.Chave` (NFC, trim, espaços colapsados, sem acento, minúsculas): `"lixo e limpeza"` casa com `"Lixo e limpeza"`. **String vazia ou só com espaços é erro** (`McpException`), não é ignorada (decisão 12) |
| `data_abertura_de`, `data_abertura_ate` | texto `yyyy-MM-dd` | Intervalo **fechado** (os dois extremos entram). Formato estrito e data existente. `de` não pode ser posterior a `ate`. Compara com `data_abertura` (texto `yyyy-MM-dd`, o que mantém a ordenação correta). Uma string vazia cai no erro de formato |

Linhas com `distrito`, `prefeitura_operacional` ou `meio_comunicacao` nulos nunca casam com um filtro por essas colunas.

### 6.3 Catálogo e filtro por Chave

Na partida, o servidor lê, para cada uma das oito colunas categóricas, os valores **não nulos com 5 ou mais linhas** no banco inteiro (sem filtros) e monta o mapa **Chave para a lista de valores originais**. Um filtro é convertido em `IN (valores originais)` com parâmetros, ou seja, a consulta usa os valores exatos do banco e o esquema não muda (decisão 5).

- **Valores com menos de 5 linhas ficam fora do mapa** e das listas de "categorias mais próximas" dos erros (decisão 13). Filtrar por um deles dá o mesmo erro de "valor desconhecido" de um valor que não existe, para a mensagem não revelar que ele existe. As linhas desses valores continuam contando nos totais e nos grupos (onde caem em "outros (agrupados)", §8).
- Medido no `.db` do arquivo real (2026-11-12): em nenhuma das oito colunas dois valores distintos têm a mesma Chave (0 colisões). Se houver colisão em outro banco, o filtro casa **todos** os valores originais daquela Chave.

### 6.4 O campo `limitacoes`

Lista de textos devolvida por **todas** as ferramentas. Decisão 14: os **textos são constantes fixas no código, cobertas por teste**; os **números e as datas são calculados do banco** a cada consulta, sem tabela de metadados e sem mudança de esquema. Textos comuns, com os valores calculados entre chaves:

1. **Cobertura:** "Os dados do SP156 se referem exclusivamente às solicitações registradas pela população. A Prefeitura pode executar um serviço sem ter recebido a solicitação correspondente; por isso o volume de pedidos não mede a quantidade de serviços executados nem os problemas existentes na cidade." (sem número.)
2. **Período:** "Este conjunto cobre apenas as solicitações abertas de {data_abertura_minima} a {data_abertura_maxima}. Não permite concluir nada sobre outros períodos." Valores: menor e maior `data_abertura`.
3. **Sem deduplicação:** "A unidade é a linha registrada. Os dados não têm ID de protocolo e {percentual_duplicatas}% das linhas ({linhas_duplicadas} de {total_linhas}) são idênticas a outra linha em todas as colunas guardadas; as contagens são de linhas registradas, não de problemas ou de pessoas distintas." Valores: `linhas_duplicadas` é o total de linhas menos o número de combinações distintas das 10 colunas; o percentual tem duas casas e vírgula decimal.
4. **Status:** "O status é um retrato de data desconhecida, não anterior a {data_finalizacao_maxima} (data da última finalização no arquivo); a data de extração não foi medida." Valor: maior `data_finalizacao`.

**Medido no `.db` do arquivo real (2026-11-12):** período `2025-01-01` a `2025-03-31`; maior finalização `2026-08-05`; **419.529 linhas duplicadas de 742.904 (56,47%)**, com 323.375 combinações distintas das 10 colunas. Esse percentual **não é o 47,65%** que o README, o `DATA.md` e o ADR 0003 citam. A diferença provável é o conjunto de colunas (o `.db` não tem as 5 colunas de endereço, e linhas que só diferiam por elas viram duplicatas), mas **não verifiquei como o 47,65% foi calculado**; a spec usa o valor calculado do banco, e os documentos citados ficam para o autor revisar (§15).

**Custo e cache (decisão 16):** o cálculo das duplicatas levou 2,2 s no Python. Como o `.db` é somente leitura e não muda durante a vida do processo, o resultado é calculado na primeira vez que for preciso e guardado em memória; o período e as datas custam pouco (0,08 s).

**Formatação (M2-3):** percentual com 2 casas, vírgula decimal e arredondamento para longe de zero (decisão 18: 3,125% vira `3,13`); contagens sem separador de milhar (`419529 de 742904`); datas `yyyy-MM-dd`. Na fixture v2: período `2025-01-06` a `2025-03-18`, última finalização `2025-03-28`, 3 duplicatas de 30 (`10,00%`).

Textos específicos de cada ferramenta estão em §8 e §9.

### 6.5 Convenções do JSON

- Nomes em `snake_case` ASCII, sem acento.
- Datas como `yyyy-MM-dd`, contagens como inteiros, ausência como `null`.
- Um **grupo** é `{ "valor": "<texto>", "pedidos": <inteiro> }`.
- **Proposta (a confirmar):** o retorno é um tipo (`record`) com `UseStructuredContent = true`, e os testes leem `StructuredContent`. Os nomes das propriedades são fixados com `[JsonPropertyName]`, se o SDK os respeitar (ver §15).
- **Grupos agrupados:** a linha "outros (agrupados)" tem o mesmo formato de um grupo: `{ "valor": "outros (agrupados)", "pedidos": <inteiro> }` (§8).

## 7. Ferramenta `descrever_dataset`

**Descrição para o modelo (proposta):** "Descreve o conjunto de dados carregado: total de linhas, período, contagem por status e por mês, número de valores distintos por coluna categórica e as limitações. Use antes de fazer perguntas, para saber o que o conjunto cobre."

**Parâmetros:** nenhum.

**Retorno** (exemplo com o arquivo do 1º trimestre de 2025):

```json
{
  "total_linhas": 742904,
  "periodo": { "data_abertura_minima": "2025-01-01", "data_abertura_maxima": "2025-03-31" },
  "por_status": [
    { "valor": "Finalizada", "pedidos": 715139 },
    { "valor": "Aberto", "pedidos": 23504 },
    { "valor": "Cancelada", "pedidos": 3948 },
    { "valor": "Em Andamento", "pedidos": 313 }
  ],
  "por_mes": [
    { "valor": "2025-01", "pedidos": 260541 },
    { "valor": "2025-02", "pedidos": 247718 },
    { "valor": "2025-03", "pedidos": 234645 }
  ],
  "valores_distintos": {
    "tema": 28, "assunto": 204, "servico": 707, "status": 4, "orgao": 517,
    "prefeitura_operacional": 38, "distrito": 96, "meio_comunicacao": 6
  },
  "linhas_sem_valor": { "prefeitura_operacional": 367648, "distrito": 374346, "meio_comunicacao": 1 },
  "limitacoes": [ "...os 4 textos comuns da §6.4..." ]
}
```

- `por_status` e `por_mes` são saídas **agrupadas**: a supressão da decisão 13 se aplica (grupos com menos de 5 pedidos viram uma linha "outros (agrupados)"). No arquivo real nenhum grupo é pequeno. Na fixture de 30 linhas, só `Finalizada` (27) passa de 5; os outros três status (1 pedido cada) viram `outros (agrupados)` com 3.
- `por_status` em ordem decrescente de `pedidos` (empate: `valor` crescente); `por_mes` em ordem cronológica (decisão 17). O desempate usa a ordem ordinal do .NET (UTF-16), não a do SQLite (bytes UTF-8); as duas só diferem com caracteres fora do BMP, e há teste para isso.
- `valores_distintos` conta **todos** os valores distintos não nulos (um agregado, que não revela valor nenhum): 517, 707, 204, 96, 38, 28, 4 e 6. `valores_no_catalogo` (decisão 15) conta os com 5 ou mais linhas: 430, 563, 182, 96, 36, 27, 4 e 6 no arquivo real (confirmado no opt-in da M2-3), para o cliente saber quantos podem ser usados como filtro. Na fixture v2: distintos 7/8/8/4/7/5/5/6 e no catálogo 2/1/1/1/2/1/1/1 (na ordem tema, assunto, servico, status, orgao, prefeitura_operacional, distrito, meio_comunicacao).
- `linhas_sem_valor` (decisão 11) traz as contagens de nulos das colunas que admitem nulo: `prefeitura_operacional`, `distrito` e `meio_comunicacao`. O `DATA.md` exige informar quantas linhas ficaram sem distrito em qualquer resposta por distrito.

**Erros:** nenhum de entrada (não há parâmetros). Falha de leitura do banco é exceção inesperada (§10).

## 8. Ferramenta `contar_pedidos`

**Descrição para o modelo (proposta):** "Conta pedidos (linhas registradas) com filtros opcionais e, se pedido, agrupados por uma dimensão. Devolve só contagens, nunca pedidos individuais. Grupos com menos de 5 pedidos aparecem somados em 'outros (agrupados)'."

**Parâmetros**

| Parâmetro | Tipo | Padrão | Regra |
|---|---|---|---|
| `tema`, `assunto`, `servico`, `status`, `orgao`, `prefeitura_operacional`, `distrito`, `meio_comunicacao` | texto | ausente | Filtros da §6.2 |
| `data_abertura_de`, `data_abertura_ate` | texto `yyyy-MM-dd` | ausente | Filtros da §6.2 |
| `agrupar_por` | texto | ausente | Um de `tema`, `assunto`, `servico`, `status`, `orgao`, `prefeitura_operacional`, `distrito`, `meio_comunicacao` e `mes` (`mes` é `yyyy-MM` de `data_abertura`). Decisão 10. Aceita também a Chave do nome (por exemplo `"Mes"`): **proposta (a confirmar)** |
| `limite` | inteiro | 20 | De 1 a 100. Número máximo de grupos devolvidos (sem contar a linha "outros (agrupados)") |

**Retorno** (exemplo: `tema = "Lixo e limpeza"`, `agrupar_por = "mes"`):

```json
{
  "total": 79632,
  "agrupar_por": "mes",
  "grupos": [
    { "valor": "2025-01", "pedidos": 26697 },
    { "valor": "2025-02", "pedidos": 27920 },
    { "valor": "2025-03", "pedidos": 25015 }
  ],
  "total_de_grupos": 3,
  "sem_valor": 0,
  "limitacoes": [ "...os 4 textos comuns da §6.4..." ]
}
```

- `total` é **exato**: conta todas as linhas que passam nos filtros, mesmo as que ficam fora dos grupos devolvidos por causa do `limite` ou da supressão.
- Sem `agrupar_por`, não há `grupos`, `total_de_grupos` nem `sem_valor`, e o `limite` é ignorado (ainda assim validado). **Proposta (a confirmar).**
- **Supressão (decisão 13).** Calculados os grupos, os que têm **menos de 5 pedidos** são somados em **uma** linha `{ "valor": "outros (agrupados)", "pedidos": N }`, que vem por último e não conta para o `limite`. Os totais se preservam: o total de todos os grupos (listados ou omitidos pelo `limite`), mais "outros (agrupados)", mais `sem_valor`, é igual a `total`. Medido no `.db` do arquivo real **sem filtros**, a linha "outros (agrupados)" valeria: `servico` 332 (144 grupos), `orgao` 179 (87 grupos), `assunto` 57 (22), `prefeitura_operacional` 3 (2), `tema` 2 (1); `status`, `distrito` e `meio_comunicacao` não têm grupo pequeno.
- Ordem dos grupos: por `pedidos` decrescente e `valor` crescente; para `mes`, cronológica. O `limite` corta depois de ordenar e depois da supressão. **Proposta (a confirmar).**
- `total_de_grupos` (decisão 11): número de grupos com 5 ou mais pedidos, antes do `limite`. **Proposta (a confirmar)** esta definição exata.
- `sem_valor` (decisão 11): linhas filtradas sem valor na coluna de agrupamento, só para as colunas que admitem nulo (`prefeitura_operacional`, `distrito`, `meio_comunicacao`); é uma contagem exata e não é um grupo.
- **Aviso específico (proposta):** quando `distrito` é filtro ou agrupamento, `limitacoes` ganha: "O distrito está ausente em {N} linhas ({P}% do total); essas linhas não entram em nenhum distrito." Aviso análogo para `prefeitura_operacional` e `meio_comunicacao`.

**Erros** (`McpException`, §10): categoria desconhecida (ou com menos de 5 linhas) em um filtro; filtro vazio ou só com espaços; data malformada ou inexistente; `data_abertura_de` posterior a `data_abertura_ate`; `agrupar_por` desconhecido; `limite` fora de 1 a 100.

## 9. Ferramenta `tempo_ate_finalizar`

**Descrição para o modelo (proposta):** "Estatísticas do tempo, em dias, entre a abertura e a finalização, só para pedidos com status Finalizada e data de finalização. Pedidos Cancelada, abertos ou em andamento não entram. Aceita os mesmos filtros de contar_pedidos."

**Parâmetros:** os dez filtros da §6.2. Não há `agrupar_por` nem `limite`.

**Semântica.** Os filtros se aplicam primeiro. Entre as linhas que passam:

- `dias` = `data_finalizacao` menos `data_abertura`, em dias inteiros. No `.db` as duas colunas são texto `yyyy-MM-dd`. A verificação por SQL desta spec usou a diferença de `julianday` truncada para inteiro e bateu com os valores do pandas (nenhum valor negativo; mínimo 0); na implementação, arredonde (`round`) antes de converter, para não depender de ponto flutuante.
- `n_com_data`: `status = Finalizada`, `data_finalizacao` não nula e `dias >= 0`. **Só essas** entram nas estatísticas.
- `n_duracao_negativa` (decisão 12): `status = Finalizada`, `data_finalizacao` não nula e `dias < 0`. Ficam **fora** das estatísticas e de `n_com_data`. Não ocorre no arquivo real (0).
- `n_finalizada_sem_data`: `status = Finalizada` e `data_finalizacao` nula.
- `n_abertas_ou_em_andamento`: `status` `Aberto` ou `Em Andamento`.
- `n_canceladas` (decisão 11): `status = Cancelada`. Ficam fora das estatísticas; o campo existe para a soma bater com o total filtrado: `n_com_data + n_duracao_negativa + n_finalizada_sem_data + n_abertas_ou_em_andamento + n_canceladas`.
- `n_mesmo_dia`: quantos de `n_com_data` têm `dias = 0`.
- `media_dias` (decisão 12): média de `dias`, com **1 casa decimal, arredondada para longe de zero** (`MidpointRounding.AwayFromZero`). Calcule com `decimal` para o arredondamento não depender de ponto flutuante.
- `p50_dias`, `p90_dias`, `p99_dias`: percentil pelo método **nearest-rank**. Com a lista de `dias` ordenada de forma crescente `x[1..n]`, o percentil `p` é `x[k]` com `k = ceil(p * n)`. Calcule `k` com inteiros: `k = (P * n + 99) / 100` em divisão inteira, onde `P` é 50, 90 ou 99, para não depender de ponto flutuante. Exemplo: `n = 714648`, `P = 99` dá `k = 707502`.
- `max_dias`: maior valor.
- Se `n_com_data = 0`, `media_dias`, os percentis e `max_dias` são `null`.
- **Sugestão de implementação (não é requisito):** o histograma `SELECT dias, count(*) ... GROUP BY dias` tem poucas linhas (575 no arquivo real, 0,34 s no Python) e permite calcular média e percentis sem trazer 715 mil linhas.

**Retorno** (exemplo sem filtros, arquivo do 1º trimestre de 2025):

```json
{
  "n_com_data": 714648,
  "n_duracao_negativa": 0,
  "n_finalizada_sem_data": 491,
  "n_abertas_ou_em_andamento": 23817,
  "n_canceladas": 3948,
  "n_mesmo_dia": 295672,
  "media_dias": 18.6,
  "p50_dias": 1,
  "p90_dias": 44,
  "p99_dias": 336,
  "max_dias": 576,
  "limitacoes": [ "...os 4 textos comuns da §6.4...", "...o aviso de censura abaixo..." ]
}
```

**Aviso de censura (texto fixo):** "Censura: pedidos ainda abertos (Aberto ou Em Andamento) e pedidos Finalizada sem data de finalização não têm tempo medido e ficam fora da média e dos percentis; qualquer média de tempo até finalizar é viesada. Pedidos Cancelada não entram na conta."

**Sem supressão aqui.** A decisão 13 cobre só saídas **agrupadas**. `tempo_ate_finalizar` devolve agregados, e com filtros muito restritivos o `n` pode ser pequeno (inclusive 1), caso em que as estatísticas revelam a duração dessa linha. É um limite conhecido (§11).

**Erros:** os mesmos de filtro de `contar_pedidos` (categoria desconhecida, filtro vazio, data malformada, intervalo invertido).

**Observação:** com um filtro `status` diferente de `Finalizada`, `n_com_data`, `n_duracao_negativa` e `n_finalizada_sem_data` são 0, e as estatísticas são `null`.

## 10. Política de erros

| Situação | Tratamento | Estado da verificação |
|---|---|---|
| Entrada inválida (categoria desconhecida ou com menos de 5 linhas, filtro vazio ou só com espaços, data malformada ou inexistente, intervalo invertido, `agrupar_por` desconhecido, `limite` fora de 1 a 100) | `McpException` com mensagem clara em português, sem conteúdo de célula. A documentação diz que a mensagem de uma `McpException` vai no texto do erro, e que o resultado é um `CallToolResult` com `IsError = true` (não um erro JSON-RPC) | Confirmado pela documentação e por sonda na M2-1: `new McpException("mensagem")` dá `IsError = true` e o texto `An error occurred invoking '<nome>': mensagem` (§4). O teste da M2-2 o reafirma para as nossas ferramentas |
| Exceção inesperada | O SDK devolve uma mensagem genérica (`An error occurred invoking '<nome>'.`), sem a mensagem da exceção | Confirmado pela documentação e por sonda na M2-1 (a mensagem `segredo-...` de uma `ArgumentException` não apareceu); M2-2 confirma por teste |
| `McpProtocolException` | **Não usamos.** Ela viraria erro JSON-RPC em vez de resultado de ferramenta, e queremos que o modelo veja o erro e se corrija | Decisão de desenho |

Mensagens previstas (proposta, texto a refinar):

- Categoria desconhecida: `Valor desconhecido para 'orgao': '<valor informado, até 100 caracteres>'. Valores válidos mais próximos: <até 5 valores>.` Os valores listados vêm só do catálogo (5 ou mais linhas).
- Filtro vazio: `'orgao' não pode ser vazio.`
- Data: `data_abertura_de deve estar no formato yyyy-MM-dd (por exemplo, 2025-02-01).`
- Intervalo: `data_abertura_de não pode ser posterior a data_abertura_ate.`
- Limite: `limite deve estar entre 1 e 100.`
- Agrupamento: `agrupar_por deve ser um de: tema, assunto, servico, status, orgao, prefeitura_operacional, distrito, meio_comunicacao, mes.`

**Valores válidos mais próximos (proposta):** os de menor distância de edição entre as Chaves (da entrada e dos valores do catálogo, ou seja, só os de 5 ou mais linhas), no máximo 5, desempatados pela ordem alfabética. A métrica e o limite de 5 são propostas.

## 11. Privacidade

- O `.db` não tem endereço nem coordenadas (ADR 0002). O servidor só lê esse banco.
- **Nenhuma ferramenta devolve linhas individuais:** só contagens, estatísticas e valores de categoria. Os testes conferem que nenhuma resposta contém uma lista de pedidos.
- **Supressão (decisão 13).** Em saídas agrupadas, grupos com menos de 5 pedidos viram uma linha "outros (agrupados)". Valores categóricos com menos de 5 linhas ficam fora do catálogo e das listas de "categorias mais próximas" nos erros. Os totais são exatos.
- **Isto NÃO é uma garantia formal de anonimato.** Limites conhecidos: (a) `total` e `sem_valor` são exatos, então filtros que isolam poucas linhas podem devolver contagens pequenas (inclusive 1); (b) diferenças entre consultas (por exemplo, o total com um filtro menos o total com o filtro mais restrito) podem revelar a contagem de um grupo pequeno; (c) `tempo_ate_finalizar` não tem supressão e, com `n` pequeno, revela a duração dessas poucas linhas; (d) a regra dos 5 não foi calibrada contra nenhum modelo de ataque. O que protege de fato é que as linhas **não têm endereço nem coordenadas**; a supressão reduz a exposição de grupos muito pequenos e de categorias raras, nada além disso.
- **Mensagens de erro sem conteúdo de célula.** Há **uma exceção deliberada**: o erro de categoria desconhecida lista os valores válidos mais próximos. Isso é permitido porque são **categorias do conjunto de dados** (órgão, serviço, assunto, distrito, prefeitura, tema, status e meio de comunicação), não dados pessoais, e vêm do catálogo carregado na partida (só valores com 5 ou mais linhas), nunca de uma linha específica. A distinção vale só para essas oito colunas; qualquer outro valor de célula continua proibido em mensagens.
- **Premissa a verificar, e o que foi medido:** que os valores distintos dessas colunas são mesmo categorias. A verificação de 2026-11-12 é uma **heurística**, **não** uma revisão manual dos 1.600 valores, e o critério importa:
  - **Critério A** (7 ou mais dígitos **consecutivos**): 1 valor distinto, 2 linhas, em `servico`; 0 nas outras colunas.
  - **Critério B** (7 ou mais caracteres de dígito **no total**, em qualquer posição): **5 valores distintos, 163 linhas, todos em `servico`; 0 nas outras colunas.** É o critério do autor, e foi reproduzido exatamente.
  - **Reconciliação:** a diferença entre A e B são 4 valores (161 linhas) cujos dígitos aparecem em grupos curtos separados (no máximo 4 dígitos seguidos), que o critério A não pega. O valor do critério A tem 8 dígitos seguidos e parece um código interno de ordem de serviço digitado no campo. Os outros quatro foram classificados só pela forma (a spec não reproduz nenhum valor) e têm formato que não corresponde a CPF, telefone ou número de protocolo, mas isso é uma inferência, não uma leitura individual.
  - **Padrões de e-mail, CPF e telefone** (expressões regulares simples): 0 valores em todas as colunas.
  - Com a regra dos 5, o valor de 2 linhas fica fora do catálogo; os outros quatro têm 10 ou mais linhas cada e entrariam. Portanto a premissa **não vale para 100% dos valores**.
- O servidor continua sujeito às limitações do Marco 1: só uma amostra do arquivo real foi checada contra vazamento de endereço, e nada se afirma sobre o que as bibliotecas decodificam em memória (ver ADR 0002).

## 12. Testes

Fluxo de sempre: teste vermelho **por asserção**, implementação mínima, mutações para provar que os testes mordem, `dotnet test -c Release` sem e com `SP156_XLSX`.

**(a) Transporte em memória** (`StreamServerTransport` e `StreamClientTransport` sobre `Pipe`, como no exemplo da documentação), sobre um `.db` gerado pelo `GravadorSqlite` a partir da **fixture v2**. Cobre o contrato de cada ferramenta (parâmetros, retorno, erros, `limitacoes`), os nomes publicados e `IsError`. O registro das nossas classes nesse modo foi resolvido na M2-1 (ver §4: `Host` com `WithStreamServerTransport` e `WithTools<T>()`).

**(b) Processo real stdio.** Dois testes:

1. **Funcional:** `StdioClientTransport` inicia `dotnet <Sp156.Mcp.dll> --db <banco>`, lista as ferramentas e chama `descrever_dataset`. `StandardErrorLines` captura o stderr, que deve ter log (prova que o log vai para lá).
2. **Pureza do stdout:** processo iniciado com `System.Diagnostics.Process`, com `stdin` e `stdout` redirecionados. O teste escreve as mensagens do protocolo no stdin, lê o stdout inteiro e afirma que **toda linha não vazia é uma mensagem JSON-RPC** (`"jsonrpc": "2.0"`) e que não há mais nada. O SDK sozinho não prova isso, porque o cliente pode ignorar lixo. O formato das mensagens foi observado numa sonda (§4: uma mensagem JSON por linha; `initialize` com `protocolVersion` `2025-11-25`); o teste da M2-6 o reproduz. A M2-1 já tem um teste de processo real mais simples: sem nenhuma requisição, o stdout fica vazio, o log vai para o stderr e o servidor sai com `0` ao fechar o stdin.

**(c) Opt-in com o arquivo real** (`FactArquivoReal`, ignorados sem `SP156_XLSX`): o `.db` é gerado a partir do XLSX apontado pela variável (como já fazem os testes do Marco 1) e as ferramentas são comparadas com os valores de referência da §13. **Proposta (a confirmar):** uma só geração do `.db` por execução de testes (fixture de coleção do xUnit), porque a geração leva 10 a 17 s.

**Camada pura (sem MCP):** catálogo, validação de entrada, supressão, montagem das consultas, o percentil nearest-rank e o arredondamento da média são testados direto, com `.db` da fixture e listas sintéticas.

**Banco sintético:** a fixture v2 tem só 30 linhas, quase todos os grupos ficam abaixo de 5. Os testes de supressão, de catálogo (valores com menos de 5 linhas) e de duração negativa usam um `.db` gerado pelo `GravadorSqlite` a partir de uma lista de `Pedido` sintéticos (por exemplo, 12 pedidos de um serviço, 4 de outro e 1 de um terceiro).

**`limitacoes`:** um teste confere que os textos constantes são exatamente os da §6.4 e outro que os números e datas vêm do banco (na fixture: período, total 30, 3 duplicatas, 10,00%).

## 13. Critérios de aceite

**Arquivo real** (`arquivo-final-1-trim-25.xlsx`, SHA-256 `cb61cd3eaa3e1a3f92fc6bbd7f2b5926408f4101ef60f9be38e8113062c35f8e`, idêntico ao registrado no `docs/DATA.md`, conferido em 2026-11-12). Valores calculados com pandas, fora do C#; **todos foram recalculados em 2026-11-12 por SQL no `.db` gerado pelo CLI do Marco 1 e bateram exatamente**.

| Verificação | Esperado |
|---|---|
| `descrever_dataset`: total de linhas | 742904 |
| `descrever_dataset`: período | 2025-01-01 a 2025-03-31 |
| `descrever_dataset`: por mês | 260541 / 247718 / 234645 |
| `descrever_dataset`: por status | Finalizada 715139, Aberto 23504, Cancelada 3948, Em Andamento 313 |
| `descrever_dataset`: valores distintos | tema 28, assunto 204, servico 707, status 4, orgao 517, prefeitura_operacional 38, distrito 96, meio_comunicacao 6 |
| `descrever_dataset`: linhas sem valor | prefeitura_operacional 367648, distrito 374346, meio_comunicacao 1 |
| `limitacoes` (texto 3) | 56,47% (419529 de 742904) |
| `limitacoes` (texto 4) | data da última finalização 2026-08-05 |
| `contar_pedidos`: `tema = "Lixo e limpeza"` | total 79632 |
| `contar_pedidos`: o mesmo tema, fevereiro | 27920 (janeiro 26697 e março 25015 vieram da verificação por SQL) |
| `contar_pedidos`: `agrupar_por = servico` sem filtros | "outros (agrupados)" 332 (144 grupos); `orgao` 179; `assunto` 57; `prefeitura_operacional` 3; `tema` 2 (valores da verificação por SQL) |
| `tempo_ate_finalizar` sem filtros | `n_com_data` 714648; `n_mesmo_dia` 295672; média 18,636 (devolvida como 18,6); p50 1; p90 44; p99 336; máx 576 |
| `tempo_ate_finalizar` sem filtros | `n_finalizada_sem_data` 491; `n_abertas_ou_em_andamento` 23817 (23504 + 313); `n_canceladas` 3948; `n_duracao_negativa` 0 |
| `tempo_ate_finalizar`, `tema = "Lixo e limpeza"` | `n_com_data` 78434; p50 2; p90 5; `n_mesmo_dia` 19834 (a verificação por SQL achou ainda média 3,742, p99 30 e máx 555, que não vieram do pandas) |

**Fixture v2** (30 linhas): `descrever_dataset` dá total 30; `por_status` é `Finalizada` 27 mais "outros (agrupados)" 3 (os outros três status têm 1 pedido cada); texto 3 de `limitacoes` com 3 duplicatas de 30 (10,00%). Para `tempo_ate_finalizar` sem filtros, `n_com_data` deve ser 26, `n_finalizada_sem_data` 1, `n_abertas_ou_em_andamento` 2 e `n_canceladas` 1. Estes números são **derivados** dos critérios da spec do Marco 1 (3 finalizações nulas: 2 de pedidos abertos ou em andamento e 1 de pedido Finalizada; status 27/1/1/1); confirmar ao implementar (M2-5).

**Geral**

- As três ferramentas aparecem em `ListToolsAsync` com descrição e esquema de parâmetros.
- Toda resposta de ferramenta tem `limitacoes` com os 4 textos comuns.
- Nenhuma resposta contém uma linha individual.
- Em saída agrupada, nenhum grupo listado tem menos de 5 pedidos, e os totais batem.
- Erros de entrada: `IsError = true` e mensagem clara sem conteúdo de célula.
- Em processo real, o stdout só tem mensagens do protocolo.

## 14. Decomposição em tarefas (ordem TDD)

Cada tarefa segue o fluxo do `AGENTS.md`: plano, teste vermelho por asserção, mínimo para o verde, 5 linhas de explicação, e parar.

| Tarefa | Conteúdo | Testes que a provam |
|---|---|---|
| **M2-1** | **Esqueleto do servidor e ADR 0008** (pacote aprovado pelo autor: `ModelContextProtocol` e `Microsoft.Extensions.Hosting` na série 10.0.x, sem a 11.0.0; ADR no formato do 0007, com versões resolvidas, licenças, nativo e NuGet audit). Criar `src/Sp156.Mcp`, adicionar à solução (editar a `.sln` à mão: o `dotnet sln add` do SDK 10 reescreve o arquivo e acrescenta configurações `x64`/`x86`) e a parte de configuração (`--db`, `SP156_DB`). Sem ferramentas ainda. Resolveu os itens "a verificar" de registro de ferramentas e de transporte em memória (§4) | Servidor sobe no transporte em memória, se apresenta como `sp156-mcp` e, sem ferramentas, não anuncia a capacidade `tools` (comportamento medido do SDK); sem `--db` e sem `SP156_DB`, ou com arquivo inexistente, termina com código `1` e mensagem no stderr; `--db` vence a variável; o banco é aberto como somente leitura (uma escrita falha) |
| **M2-2** | **Catálogo e validação de entrada** (camada pura, sem MCP): mapa Chave para originais só com valores de 5 ou mais linhas, valores mais próximos, filtro vazio, datas, `limite`, montagem parametrizada dos filtros e a supressão de grupos pequenos. Confirmar o comportamento de `McpException` | Unitários sobre `.db` da fixture e sobre `.db` sintético (valores com menos de 5 linhas fora do catálogo); nenhuma mensagem com conteúdo de célula; `McpException` e a exceção genérica confirmadas por teste; opt-in: 1.344 valores no catálogo, 256 de fora e 0 colisões no arquivo real |
| **M2-3** | **`descrever_dataset`** | Transporte em memória na fixture; opt-in com os valores da §13 |
| **M2-4** | **`contar_pedidos`** | Filtros, agrupamentos, `limite`, erros; opt-in: `Lixo e limpeza` (79632 e 27920 em fevereiro) |
| **M2-5** | **`tempo_ate_finalizar`**, com o percentil nearest-rank como função pura | Unitários do percentil (listas sintéticas, inclusive `n` pequeno e `n = 0`); fixture (26, 1, 2); opt-in com os valores da §13 |
| **M2-6** | **Teste de processo real stdio**: o funcional e o de pureza do stdout | Os dois testes da §12(b) |
| **M2-7** | **README e demonstração**: como configurar um cliente MCP, exemplos de perguntas e respostas reais, limites. CHANGELOG. Decidir com o autor se há tag | Revisão do texto contra o que foi medido |

Mutações previstas por tarefa (exemplos): tirar o `limitacoes` de uma ferramenta; trocar nearest-rank por outro método; arredondar a média para o par em vez de para longe de zero; aceitar `limite` 0 ou 101; aceitar filtro vazio; deixar o catálogo casar por igualdade exata em vez de Chave; baixar o limiar da supressão de 5 para 1; incluir valores raros no catálogo; escrever no stdout.

## 15. Perguntas em aberto e itens a verificar no código

**Confirmado na M2-1** (detalhes na §4): construtores de `McpException`; nome publicado das ferramentas (`snake_case` do método) e `Name` no atributo; nomes JSON na saída (`[JsonPropertyName]` respeitado; sem ele, `camelCase`) e o bloco de texto que acompanha a saída estruturada; parâmetros opcionais e tipo errado; registro das nossas classes no transporte em memória; comportamento ao fechar o stdin; versões e dependências (ADR 0008).

**Confirmado na M2-2** (testes em `ErrosDoSdkTests`, transporte em memória, SDK 2.4.0):

- `new McpException("mensagem clara")` dá `IsError = true` e o texto `An error occurred invoking 'falha_clara': mensagem clara`.
- Exceção inesperada (`ArgumentException("segredo-123")`) dá `IsError = true` e só `An error occurred invoking 'falha_inesperada'.`, sem o texto da exceção.
- Parâmetro **obrigatório ausente**, **texto no lugar de inteiro** e **número no lugar de texto** dão `IsError = true` com a mesma mensagem genérica (`An error occurred invoking 'eco'.`). A mensagem não diz qual parâmetro está errado, então o modelo não tem como se corrigir sozinho. Consequência proposta para as M2-3 a M2-5: declarar todos os parâmetros das ferramentas como opcionais e anuláveis e validar no nosso código, para que o erro venha como `McpException` com mensagem clara.
- Parâmetro **desconhecido** continua ignorado em silêncio (a chamada com `orgão` roda como se o filtro não existisse). Não investiguei se dá para rejeitá-lo (item 2 abaixo).

Aplicado na M2-2 como proposta, sem decisão do autor: valores mais próximos pela distância de edição sobre a Chave, até 5, desempate em ordem ordinal (decisão em aberto 5); quando a coluna não tem nenhum valor no catálogo, a lista diz `nenhum`; `agrupar_por` aceita a Chave do nome (`"Mes"`, `"mês"`), como proposto na §8.

**Confirmado na M2-3** (`descrever_dataset`):

- Com uma ferramenta registrada, o servidor anuncia `tools` e lista `descrever_dataset` sem abrir o banco. O teste "sem ferramentas" da M2-1 foi substituído por esse, com aprovação do autor.
- Um método sem parâmetros publica um esquema de entrada sem propriedades, e `UseStructuredContent = true` publica `OutputSchema`.
- A resposta tem exatamente as 8 propriedades da §7 (com `valores_no_catalogo`), todas em `snake_case`.
- Opt-in no arquivo real: todos os valores da §13 para `descrever_dataset` bateram.

Aplicado na M2-3 sem decisão explícita do autor: carga do catálogo na primeira chamada, não na partida (§5).

**Ainda a verificar no código (SDK 2.4.0)**

1. Pureza do stdout numa conversa de protocolo completa, inclusive quando o SQLite nativo ou uma exceção não tratada escrevem algo (M2-6).
2. Se é possível rejeitar parâmetros desconhecidos (o SDK os ignora; ver a primeira decisão em aberto abaixo): filtro de requisição, validação do `JsonElement` ou outra forma. Não investigado na M2-2; fica para a M2-4, a primeira ferramenta com filtros.
3. ~~O texto exato de erro para parâmetros obrigatórios ausentes e para tipos errados~~: medido na M2-2 (acima).

**Decisões em aberto (para o autor)**

0. **Parâmetro desconhecido é ignorado em silêncio** (medido na sonda). Se o modelo escrever o nome de um filtro errado (por exemplo `orgão` em vez de `orgao`), a chamada roda **sem o filtro** e devolve a contagem do conjunto inteiro, o que parece uma resposta correta. Como mitigar: rejeitar parâmetros desconhecidos com `McpException` (se o SDK permitir, ver a verificação 2) e/ou devolver `filtros_aplicados` em toda resposta para o modelo conferir o que valeu.
1. **47,65% contra 56,47%.** O `.db` tem 56,47% de linhas duplicadas nas 10 colunas guardadas, e o README, o `DATA.md` e o ADR 0003 dizem 47,65%. Como o 47,65% foi calculado (quais colunas)? Esses três documentos precisam de revisão; esta spec usa o valor calculado do banco.
2. `total_de_grupos`: confirmar a definição (grupos com 5 ou mais pedidos, antes do `limite`) e que a linha "outros (agrupados)" não conta para o `limite`.
3. ~~`valores_no_catalogo` em `descrever_dataset`: entra?~~ Sim (decisão 15).
4. ~~Guardar em memória o cálculo das duplicatas (2,2 s) na primeira vez em que for preciso?~~ Sim (decisão 16).
5. Valores mais próximos: métrica (distância de edição sobre a Chave) e limite de 5.
6. Validar o esquema do `.db` na partida e falhar com código `1`?
7. `tempo_ate_finalizar` sem supressão (§9, §11): aceitar o limite conhecido ou suprimir quando `n` for pequeno?
8. Textos de aviso específicos de `contar_pedidos` (distrito, prefeitura, meio) e de `limitacoes`: revisar a redação.

## 16. Referências

- SDK C# do MCP, documentação v2 (consultada em 2026-11-12): [getting-started](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/getting-started.html), [tools](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/tools/tools.html), [transports](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html).
- [Spec do Marco 1](spec-m1-ingestao.md), [dados](DATA.md), [ADR 0002](decisions/0002-privacidade-sem-enderecos.md), [ADR 0003](decisions/0003-nao-deduplicar-linhas.md), [ADR 0007](decisions/0007-usar-microsoft-data-sqlite.md).
