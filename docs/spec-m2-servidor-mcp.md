# Spec M2: Servidor MCP do SP156

> **Status: rascunho de especificação (tarefa M2-0), escrito em 2026-11-12. Nenhum código do Marco 2 existe.**

Convenções deste documento:

- **Decisão:** já tomada pelo autor do projeto. Não reabrir sem avisar.
- **Proposta (a confirmar):** sugerida nesta spec, ainda sem decisão.
- **A verificar no código:** a documentação do SDK consultada não confirma. Nada disso é afirmado aqui; cada item vira um teste ou uma leitura do código na tarefa indicada.

## 1. Objetivo

Expor, por MCP sobre stdio, consultas **agregadas** (contagens e estatísticas) sobre o banco SQLite gerado no Marco 1, sempre acompanhadas das limitações dos dados. O servidor nunca devolve linhas individuais.

## 2. Escopo e fora de escopo

**Dentro do escopo**

- Servidor MCP sobre **stdio**, em `src/Sp156.Mcp` (console, `net10.0`).
- Pacotes `ModelContextProtocol` (2.4.0) e `Microsoft.Extensions.Hosting`.
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
| 4 | Três ferramentas, todas devolvendo, além do dado, o campo `limitacoes` | §6 a §9 |
| 5 | Filtros categóricos casam por `TextoNormalizado.Chave`. Na partida, o servidor carrega os valores distintos de cada coluna categórica e monta o mapa Chave para valores originais; a consulta usa os valores originais exatos. Sem mudança de esquema | §6.3 |
| 6 | Valor de filtro desconhecido: erro com a lista dos valores válidos mais próximos (permitido: são categorias do conjunto de dados, não dados pessoais). Nenhuma ferramenta devolve linhas individuais, só contagens e estatísticas | §10, §11 |
| 7 | Entrada inválida: `McpException` com mensagem clara e sem conteúdo de célula. Exceção inesperada: mensagem genérica do SDK. O comportamento exato é confirmado por teste | §10 |
| 8 | Testes em 3 camadas: transporte em memória, processo real stdio e opt-in com o arquivo real | §12 |
| 9 | Valores de referência calculados com pandas sobre o mesmo arquivo | §13 |

## 4. O que a documentação do SDK confirma

Fontes consultadas em 2026-11-12 (documentação v2 do SDK C#):
[getting-started](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/getting-started.html),
[tools](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/tools/tools.html) e
[transports](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html).

| Assunto | O que a documentação confirma | O que fica a verificar |
|---|---|---|
| Pacotes | `ModelContextProtocol` é o ponto de partida para um servidor **stdio** (traz hosting, injeção de dependência e descoberta por atributos) e referencia `ModelContextProtocol.Core`. Instalação: `dotnet add package ModelContextProtocol` e `dotnet add package Microsoft.Extensions.Hosting` | Versão do `Microsoft.Extensions.Hosting` compatível com `net10.0` e dependências transitivas: ADR 0008 (M2-1). A versão 2.4.0 do `ModelContextProtocol` (publicada em 07/11/2026, alvos `net10.0`, `net8.0`, `net9.0` e `netstandard2.0`, licença Apache-2.0) foi conferida na API do NuGet, não nestas páginas |
| Servidor stdio | `Host.CreateApplicationBuilder(args)`, `builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` (ou `.WithTools<T>()`), `await builder.Build().RunAsync()` | Comportamento ao fechar o stdin (encerra? código de saída?) |
| Logs | O exemplo oficial usa `builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)` com o comentário "Configure all logs to go to stderr" | Que **nada mais** escreve no stdout (as mensagens do próprio host, de bibliotecas nativas etc.): teste de processo real (M2-6) |
| Ferramenta por atributos | `[McpServerToolType]` na classe, `[McpServerTool, Description("...")]` no método, `[Description]` nos parâmetros. Os parâmetros são desserializados do JSON, e o valor padrão (`int maxResults = 10`) aparece no esquema. Um `string` devolvido vira `TextContentBlock` | Nome exato da ferramenta publicada (os exemplos sugerem snake_case a partir do nome do método, por exemplo `Divide` vira `divide`) e se o atributo aceita `Name`; se `string? x = null` fica opcional no esquema; o que acontece com tipo errado no JSON |
| Saída estruturada | `UseStructuredContent = true` no atributo (ou em `McpServerToolCreateOptions`) publica um esquema de saída e serializa o retorno em `StructuredContent`. Um valor que não é objeto não é embrulhado em `result` | Nomes das propriedades JSON na saída (se respeita `[JsonPropertyName]` ou usa uma política própria); se, com `UseStructuredContent`, também é enviado um bloco de texto |
| Erros | Exceção lançada pela ferramenta vira `CallToolResult` com `IsError = true`. Se for `McpException` (exceto `McpProtocolException`), a mensagem entra no texto do erro; qualquer outra exceção vira uma mensagem genérica (no exemplo da documentação: `An error occurred invoking 'divide'.`). `McpProtocolException` vira erro JSON-RPC (exemplo: `-32602 InvalidParams`). `OperationCanceledException` é relançada se o token foi cancelado | Construtor e texto exato de `McpException`; formato do texto do erro; ver §10 |
| Transporte em memória | `StreamServerTransport` sobre `Pipe`, `McpServer.Create(transport, new McpServerOptions { ToolCollection = [McpServerTool.Create(...)] })`, `server.RunAsync()`, e no cliente `McpClient.CreateAsync(new StreamClientTransport(...))`, `ListToolsAsync`, `CallToolAsync` | Como registrar **nossas classes** de ferramentas (que dependem de serviços) nesse modo: o exemplo só usa delegates |
| Cliente stdio | `StdioClientTransport(new StdioClientTransportOptions { Command, Arguments, WorkingDirectory, EnvironmentVariables, InheritEnvironmentVariables (padrão `true`), ShutdownTimeout (padrão 5 s), StandardErrorLines, Name })` e `McpClient.CreateAsync(transport)` | Enquadramento das mensagens no stdio (uma mensagem JSON por linha?): vem da especificação do protocolo, não destas páginas; M2-6 confere lendo o stdout cru |

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
- **Partida:** o servidor carrega o catálogo da §6.3 antes de aceitar chamadas. Medida de ordem de grandeza (Python com SQLite 3.45.1, não o C#): 7 consultas `SELECT DISTINCT` levaram 0,93 s no `.db` do arquivo real. O `.db` não tem índices, e o servidor não pode criá-los (somente leitura).

## 6. Vocabulário comum das ferramentas

### 6.1 Colunas categóricas

`tema`, `assunto`, `servico`, `status`, `orgao`, `prefeitura_operacional` e `distrito`. Valores distintos medidos no `.db` do arquivo real em 2026-11-12: `orgao` 517, `servico` 707, `assunto` 204, `distrito` 96, `prefeitura_operacional` 38, `tema` 28 e `status` 4 (1.594 no total). `meio_comunicacao` (6 valores, ausente em 1 linha) **não** é filtro nem agrupamento no M2 (ver §15).

### 6.2 Filtros

Todos opcionais, combinados com **E**.

| Parâmetro | Tipo | Regra |
|---|---|---|
| `tema`, `assunto`, `servico`, `status`, `orgao`, `prefeitura_operacional`, `distrito` | texto | Casa por `TextoNormalizado.Chave` (NFC, trim, espaços colapsados, sem acento, minúsculas): `"lixo e limpeza"` casa com `"Lixo e limpeza"` |
| `data_abertura_de`, `data_abertura_ate` | texto `yyyy-MM-dd` | Intervalo **fechado** (os dois extremos entram). Formato estrito e data existente. `de` não pode ser posterior a `ate`. Compara com `data_abertura` (texto `yyyy-MM-dd`, o que mantém a ordenação correta) |

Linhas com `distrito` ou `prefeitura_operacional` nulos nunca casam com um filtro por essas colunas.

### 6.3 Catálogo e filtro por Chave

Na partida, o servidor lê os valores distintos (não nulos) das sete colunas categóricas e monta, por coluna, o mapa **Chave para a lista de valores originais**. Um filtro é convertido em `IN (valores originais)` com parâmetros, ou seja, a consulta usa os valores exatos do banco e o esquema não muda.

Medido no `.db` do arquivo real (2026-11-12): em nenhuma das sete colunas dois valores distintos têm a mesma Chave (0 colisões). Se houver colisão em outro banco, o filtro casa **todos** os valores originais daquela Chave.

### 6.4 O campo `limitacoes`

Lista de textos devolvida por **todas** as ferramentas. Textos comuns:

1. **Cobertura:** "Os dados do SP156 se referem exclusivamente às solicitações registradas pela população. A Prefeitura pode executar um serviço sem ter recebido a solicitação correspondente; por isso o volume de pedidos não mede a quantidade de serviços executados nem os problemas existentes na cidade."
2. **Período:** "Este conjunto cobre apenas as solicitações abertas de {data_abertura_minima} a {data_abertura_maxima}, ou seja, um trimestre. Não permite concluir nada sobre outros períodos."
3. **Sem deduplicação:** "A unidade é a linha registrada. Os dados não têm ID de protocolo e, no arquivo do 1º trimestre de 2025, 47,65% das linhas são duplicatas exatas de outra; as contagens são de linhas registradas, não de problemas ou de pessoas distintas."
4. **Status:** "O status é um retrato de data desconhecida, não anterior a {data_finalizacao_maxima} (data da última finalização no arquivo); a data de extração não foi medida."

**Proposta (a confirmar):** `{data_abertura_minima}`, `{data_abertura_maxima}` e `{data_finalizacao_maxima}` são lidas do próprio banco na partida (medidas no arquivo real: `2025-01-01`, `2025-03-31` e `2026-08-05`), em vez de escritas no código.

Textos específicos estão em §8 e §9.

### 6.5 Convenções do JSON

- Nomes em `snake_case` ASCII, sem acento.
- Datas como `yyyy-MM-dd`, contagens como inteiros, ausência como `null`.
- Um **grupo** é `{ "valor": "<texto>", "pedidos": <inteiro> }`.
- **Proposta (a confirmar):** o retorno é um tipo (`record`) com `UseStructuredContent = true`, e os testes leem `StructuredContent`. Os nomes das propriedades são fixados com `[JsonPropertyName]`, se o SDK os respeitar (ver §15).

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
    "tema": 28, "assunto": 204, "servico": 707, "status": 4,
    "orgao": 517, "prefeitura_operacional": 38, "distrito": 96
  },
  "limitacoes": [ "...os 4 textos comuns da §6.4..." ]
}
```

- `por_status` em ordem decrescente de `pedidos` (empate: `valor` crescente); `por_mes` em ordem cronológica. **Proposta (a confirmar):** a ordenação é essa, para o resultado ser determinístico.
- `valores_distintos` conta só valores não nulos.
- **Proposta (a confirmar):** incluir `linhas_sem_valor` com as contagens de nulos de `prefeitura_operacional` e `distrito` (medidas: 367.648 e 374.346 no arquivo real), porque o `DATA.md` exige informar quantas linhas ficaram sem distrito em qualquer resposta por distrito.

**Erros:** nenhum de entrada (não há parâmetros). Falha de leitura do banco é exceção inesperada (§10).

## 8. Ferramenta `contar_pedidos`

**Descrição para o modelo (proposta):** "Conta pedidos (linhas registradas) com filtros opcionais e, se pedido, agrupados por uma dimensão. Devolve só contagens, nunca pedidos individuais."

**Parâmetros**

| Parâmetro | Tipo | Padrão | Regra |
|---|---|---|---|
| `tema`, `assunto`, `servico`, `status`, `orgao`, `prefeitura_operacional`, `distrito` | texto | ausente | Filtros da §6.2 |
| `data_abertura_de`, `data_abertura_ate` | texto `yyyy-MM-dd` | ausente | Filtros da §6.2 |
| `agrupar_por` | texto | ausente | Um de `tema`, `assunto`, `status`, `orgao`, `distrito`, `mes` (`mes` é `yyyy-MM` de `data_abertura`). Aceita também a Chave do nome (por exemplo `"Mes"`): **proposta (a confirmar)** |
| `limite` | inteiro | 20 | De 1 a 100. Número máximo de grupos devolvidos |

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
  "limitacoes": [ "...os 4 textos comuns da §6.4..." ]
}
```

- `total` conta **todas** as linhas que passam nos filtros, mesmo as que ficam fora dos grupos devolvidos por causa do `limite`.
- Sem `agrupar_por`, não há `grupos` e o `limite` é ignorado (ainda assim validado). **Proposta (a confirmar).**
- Ordem dos grupos: por `pedidos` decrescente e `valor` crescente; para `mes`, cronológica. O `limite` corta depois de ordenar. **Proposta (a confirmar).**
- **Proposta (a confirmar):** além de `total` e `grupos`, devolver `total_de_grupos` (grupos distintos antes do `limite`) e, quando a coluna de agrupamento admite nulos (`distrito`), `sem_valor` (linhas filtradas sem valor naquela coluna).
- **Aviso específico (proposta):** quando `distrito` é filtro ou agrupamento, `limitacoes` ganha: "O distrito está ausente em {N} linhas ({P}% do total); essas linhas não entram em nenhum distrito." Aviso análogo para `prefeitura_operacional`.

**Erros** (`McpException`, §10): categoria desconhecida em um filtro; data malformada ou inexistente; `data_abertura_de` posterior a `data_abertura_ate`; `agrupar_por` desconhecido; `limite` fora de 1 a 100.

## 9. Ferramenta `tempo_ate_finalizar`

**Descrição para o modelo (proposta):** "Estatísticas do tempo, em dias, entre a abertura e a finalização, só para pedidos com status Finalizada e data de finalização. Pedidos Cancelada, abertos ou em andamento não entram. Aceita os mesmos filtros de contar_pedidos."

**Parâmetros:** os nove filtros da §6.2. Não há `agrupar_por` nem `limite`.

**Semântica.** Os filtros se aplicam primeiro. Entre as linhas que passam:

- `n_com_data`: `status = Finalizada` e `data_finalizacao` não nula. **Só essas** entram nas estatísticas.
- `n_finalizada_sem_data`: `status = Finalizada` e `data_finalizacao` nula.
- `n_abertas_ou_em_andamento`: `status` `Aberto` ou `Em Andamento`.
- `Cancelada` fica de fora de tudo.
- `dias` = `data_finalizacao` menos `data_abertura`, em dias inteiros. No `.db` as duas colunas são texto `yyyy-MM-dd`. A verificação por SQL desta spec usou a diferença de `julianday` truncada para inteiro e bateu com os valores do pandas (nenhum valor negativo; mínimo 0); na implementação, arredonde (`round`) antes de converter, para não depender de ponto flutuante.
- `n_mesmo_dia`: quantos de `n_com_data` têm `dias = 0`.
- `media_dias`: média de `dias`, com **1 casa decimal**.
- `p50_dias`, `p90_dias`, `p99_dias`: percentil pelo método **nearest-rank**. Com a lista de `dias` ordenada de forma crescente `x[1..n]`, o percentil `p` é `x[k]` com `k = ceil(p * n)`. Calcule `k` com inteiros: `k = (P * n + 99) / 100` em divisão inteira, onde `P` é 50, 90 ou 99, para não depender de ponto flutuante. Exemplo: `n = 714648`, `P = 99` dá `k = 707502`.
- `max_dias`: maior valor.
- Se `n_com_data = 0`, `media_dias`, os percentis e `max_dias` são `null`.
- **Sugestão de implementação (não é requisito):** o histograma `SELECT dias, count(*) ... GROUP BY dias` tem poucas linhas (575 no arquivo real, 0,34 s no Python) e permite calcular média e percentis sem trazer 715 mil linhas.

**Retorno** (exemplo sem filtros, arquivo do 1º trimestre de 2025):

```json
{
  "n_com_data": 714648,
  "n_finalizada_sem_data": 491,
  "n_abertas_ou_em_andamento": 23817,
  "n_mesmo_dia": 295672,
  "media_dias": 18.6,
  "p50_dias": 1,
  "p90_dias": 44,
  "p99_dias": 336,
  "max_dias": 576,
  "limitacoes": [ "...os 4 textos comuns da §6.4...", "...o aviso de censura abaixo..." ]
}
```

**Aviso de censura (texto proposto):** "Censura: pedidos ainda abertos (Aberto ou Em Andamento) e pedidos Finalizada sem data de finalização não têm tempo medido e ficam fora da média e dos percentis; qualquer média de tempo até finalizar é viesada. Pedidos Cancelada não entram na conta."

**Erros:** os mesmos de filtro de `contar_pedidos` (categoria desconhecida, data malformada, intervalo invertido).

**Observação:** com um filtro `status` diferente de `Finalizada`, `n_com_data` e `n_finalizada_sem_data` são 0, e as estatísticas são `null`.

## 10. Política de erros

| Situação | Tratamento | Estado da verificação |
|---|---|---|
| Entrada inválida (categoria desconhecida, data malformada ou inexistente, intervalo invertido, `agrupar_por` desconhecido, `limite` fora de 1 a 100) | `McpException` com mensagem clara em português, sem conteúdo de célula. A documentação diz que a mensagem de uma `McpException` vai no texto do erro, e que o resultado é um `CallToolResult` com `IsError = true` (não um erro JSON-RPC) | Confirmado pela documentação; construtor e texto exato **a verificar no código** (M2-2) |
| Exceção inesperada | O SDK devolve uma mensagem genérica (a documentação mostra `An error occurred invoking '<nome>'.`), sem a mensagem da exceção | Confirmado pela documentação; M2-2 confirma por teste |
| `McpProtocolException` | **Não usamos.** Ela viraria erro JSON-RPC em vez de resultado de ferramenta, e queremos que o modelo veja o erro e se corrija | Decisão de desenho |

Mensagens previstas (proposta, texto a refinar):

- Categoria desconhecida: `Valor desconhecido para 'orgao': '<valor informado, até 100 caracteres>'. Valores válidos mais próximos: <até 5 valores>.`
- Data: `data_abertura_de deve estar no formato yyyy-MM-dd (por exemplo, 2025-02-01).`
- Intervalo: `data_abertura_de não pode ser posterior a data_abertura_ate.`
- Limite: `limite deve estar entre 1 e 100.`
- Agrupamento: `agrupar_por deve ser um de: tema, assunto, status, orgao, distrito, mes.`

**Valores válidos mais próximos (proposta):** os de menor distância de edição entre as Chaves (da entrada e dos valores do catálogo), no máximo 5, desempatados pela ordem alfabética. A métrica e o limite de 5 são propostas.

## 11. Privacidade

- O `.db` não tem endereço nem coordenadas (ADR 0002). O servidor só lê esse banco.
- **Nenhuma ferramenta devolve linhas individuais:** só contagens, estatísticas e valores de categoria. Os testes conferem que nenhuma resposta contém uma lista de pedidos.
- **Mensagens de erro sem conteúdo de célula.** Há **uma exceção deliberada**: o erro de categoria desconhecida lista os valores válidos mais próximos. Isso é permitido porque são **categorias do conjunto de dados** (órgão, serviço, assunto, distrito, prefeitura, tema, status), não dados pessoais, e vêm do catálogo carregado na partida, nunca de uma linha específica. A distinção vale só para essas sete colunas; qualquer outro valor de célula continua proibido em mensagens.
- **Premissa a verificar:** que os valores distintos dessas sete colunas são mesmo categorias. Verificação feita em 2026-11-12 (grosseira, **não** uma revisão manual dos 1.594 valores): nenhum valor casa com padrão de e-mail, CPF ou telefone, e um valor de `servico` (2 linhas) tem 7 ou mais dígitos e parece um código interno de ordem de serviço digitado no campo, não um nome de serviço. A premissa, portanto, não vale para 100% dos valores.
- **Contagens pequenas:** uma combinação de filtros (por exemplo distrito, serviço e dia) pode isolar uma única linha. A linha não tem endereço nem coordenadas, então isso não identifica uma pessoa, mas é um ponto em aberto (§15) saber se contagens muito pequenas devem ser suprimidas.
- O servidor continua sujeito às limitações do Marco 1: só uma amostra do arquivo real foi checada contra vazamento de endereço, e nada se afirma sobre o que as bibliotecas decodificam em memória (ver ADR 0002).

## 12. Testes

Fluxo de sempre: teste vermelho **por asserção**, implementação mínima, mutações para provar que os testes mordem, `dotnet test -c Release` sem e com `SP156_XLSX`.

**(a) Transporte em memória** (`StreamServerTransport` e `StreamClientTransport` sobre `Pipe`, como no exemplo da documentação), sobre um `.db` gerado pelo `GravadorSqlite` a partir da **fixture v2**. Cobre o contrato de cada ferramenta (parâmetros, retorno, erros, `limitacoes`), os nomes publicados e `IsError`. Como registrar as nossas classes nesse modo é **a verificar no código** (M2-1).

**(b) Processo real stdio.** Dois testes:

1. **Funcional:** `StdioClientTransport` inicia `dotnet <Sp156.Mcp.dll> --db <banco>`, lista as ferramentas e chama `descrever_dataset`. `StandardErrorLines` captura o stderr, que deve ter log (prova que o log vai para lá).
2. **Pureza do stdout:** processo iniciado com `System.Diagnostics.Process`, com `stdin` e `stdout` redirecionados. O teste escreve as mensagens do protocolo no stdin, lê o stdout inteiro e afirma que **toda linha não vazia é uma mensagem JSON-RPC** (`"jsonrpc": "2.0"`) e que não há mais nada. O SDK sozinho não prova isso, porque o cliente pode ignorar lixo. O formato exato das mensagens de `initialize` e o enquadramento por linha são **a verificar no código**.

**(c) Opt-in com o arquivo real** (`FactArquivoReal`, ignorados sem `SP156_XLSX`): o `.db` é gerado a partir do XLSX apontado pela variável (como já fazem os testes do Marco 1) e as ferramentas são comparadas com os valores de referência da §13. **Proposta (a confirmar):** uma só geração do `.db` por execução de testes (fixture de coleção do xUnit), porque a geração leva 10 a 17 s.

**Camada pura (sem MCP):** catálogo, validação de entrada, montagem das consultas e o percentil nearest-rank são testados direto, com `.db` da fixture e listas sintéticas.

## 13. Critérios de aceite

**Arquivo real** (`arquivo-final-1-trim-25.xlsx`, SHA-256 `cb61cd3eaa3e1a3f92fc6bbd7f2b5926408f4101ef60f9be38e8113062c35f8e`, idêntico ao registrado no `docs/DATA.md`, conferido em 2026-11-12). Valores calculados com pandas, fora do C#; **todos foram recalculados em 2026-11-12 por SQL no `.db` gerado pelo CLI do Marco 1 e bateram exatamente**.

| Verificação | Esperado |
|---|---|
| `descrever_dataset`: total de linhas | 742904 |
| `descrever_dataset`: período | 2025-01-01 a 2025-03-31 |
| `descrever_dataset`: por mês | 260541 / 247718 / 234645 |
| `descrever_dataset`: por status | Finalizada 715139, Aberto 23504, Cancelada 3948, Em Andamento 313 |
| `descrever_dataset`: valores distintos | tema 28, assunto 204, servico 707, status 4, orgao 517, prefeitura_operacional 38, distrito 96 |
| `contar_pedidos`: `tema = "Lixo e limpeza"` | total 79632 |
| `contar_pedidos`: o mesmo tema, fevereiro | 27920 (janeiro 26697 e março 25015 vieram da verificação por SQL) |
| `tempo_ate_finalizar` sem filtros | `n_com_data` 714648; `n_mesmo_dia` 295672; média 18,636 (devolvida como 18,6); p50 1; p90 44; p99 336; máx 576 |
| `tempo_ate_finalizar` sem filtros | `n_finalizada_sem_data` 491; `n_abertas_ou_em_andamento` 23817 (23504 + 313) |
| `tempo_ate_finalizar`, `tema = "Lixo e limpeza"` | `n_com_data` 78434; p50 2; p90 5; `n_mesmo_dia` 19834 (a verificação por SQL achou ainda média 3,742, p99 30 e máx 555, que não vieram do pandas) |

**Fixture v2** (30 linhas): `descrever_dataset` dá total 30 e status 27/1/1/1. Para `tempo_ate_finalizar` sem filtros, `n_com_data` deve ser 26, `n_finalizada_sem_data` 1 e `n_abertas_ou_em_andamento` 2. Estes números são **derivados** dos critérios da spec do Marco 1 (3 finalizações nulas: 2 de pedidos abertos ou em andamento e 1 de pedido Finalizada); confirmar ao implementar (M2-5).

**Geral**

- As três ferramentas aparecem em `ListToolsAsync` com descrição e esquema de parâmetros.
- Toda resposta de ferramenta tem `limitacoes` com os 4 textos comuns.
- Nenhuma resposta contém uma linha individual.
- Erros de entrada: `IsError = true` e mensagem clara sem conteúdo de célula.
- Em processo real, o stdout só tem mensagens do protocolo.

## 14. Decomposição em tarefas (ordem TDD)

Cada tarefa segue o fluxo do `AGENTS.md`: plano, teste vermelho por asserção, mínimo para o verde, 5 linhas de explicação, e parar.

| Tarefa | Conteúdo | Testes que a provam |
|---|---|---|
| **M2-1** | **Esqueleto do servidor e ADR 0008.** Propor o pacote (`ModelContextProtocol` 2.4.0 e `Microsoft.Extensions.Hosting`, com versão, licenças, o que trazem de nativo e NuGet audit, como no ADR 0007) e esperar aprovação. Criar `src/Sp156.Mcp`, adicionar à solução (editar a `.sln` à mão: o `dotnet sln add` do SDK 10 reescreve o arquivo e acrescenta configurações `x64`/`x86`) e a parte de configuração (`--db`, `SP156_DB`). Sem ferramentas ainda. Resolver aqui os itens "a verificar" de registro de ferramentas e transporte em memória | Servidor sobe no transporte em memória e lista zero ferramentas; sem `--db` e sem `SP156_DB`, ou com arquivo inexistente, termina com código `1` e mensagem no stderr; `--db` vence a variável; o banco é aberto como somente leitura (uma escrita falha) |
| **M2-2** | **Catálogo e validação de entrada** (camada pura, sem MCP): mapa Chave para originais, valores mais próximos, datas, `limite`, montagem parametrizada dos filtros. Confirmar o comportamento de `McpException` | Unitários sobre `.db` da fixture; nenhuma mensagem com conteúdo de célula; `McpException` e a exceção genérica confirmadas por teste; opt-in: contagens distintas do arquivo real e 0 colisões |
| **M2-3** | **`descrever_dataset`** | Transporte em memória na fixture; opt-in com os valores da §13 |
| **M2-4** | **`contar_pedidos`** | Filtros, agrupamentos, `limite`, erros; opt-in: `Lixo e limpeza` (79632 e 27920 em fevereiro) |
| **M2-5** | **`tempo_ate_finalizar`**, com o percentil nearest-rank como função pura | Unitários do percentil (listas sintéticas, inclusive `n` pequeno e `n = 0`); fixture (26, 1, 2); opt-in com os valores da §13 |
| **M2-6** | **Teste de processo real stdio**: o funcional e o de pureza do stdout | Os dois testes da §12(b) |
| **M2-7** | **README e demonstração**: como configurar um cliente MCP, exemplos de perguntas e respostas reais, limites. CHANGELOG. Decidir com o autor se há tag | Revisão do texto contra o que foi medido |

Mutações previstas por tarefa (exemplos): tirar o `limitacoes` de uma ferramenta; trocar nearest-rank por outro método; aceitar `limite` 0 ou 101; deixar o catálogo casar por igualdade exata em vez de Chave; escrever no stdout.

## 15. Perguntas em aberto e itens a verificar no código

**A verificar no código (SDK 2.4.0)**

1. Construtor, tipo exato e texto de erro de `McpException`; formato do texto de `IsError` (M2-2).
2. Nome publicado da ferramenta (conversão do nome do método para `snake_case`?) e se `[McpServerTool]` aceita `Name` (M2-1).
3. Saída estruturada: nomes das propriedades JSON, se `[JsonPropertyName]` é respeitado e se também sai um bloco de texto (M2-1).
4. Parâmetros opcionais: se `string? x = null` fica opcional no esquema, o que o SDK faz com tipo errado e se rejeita parâmetro desconhecido (M2-1).
5. Registrar nossas classes de ferramenta (com serviços injetados) no transporte em memória: `McpServer.Create` com `IServiceProvider`, ou outro caminho (M2-1).
6. Comportamento ao fechar o stdin (encerra? código de saída?) e enquadramento exato das mensagens no stdout (M2-6).
7. Que o host não escreve nada no stdout além do protocolo, inclusive quando o SQLite nativo ou uma exceção não tratada escrevem algo (M2-6).
8. Versão do `Microsoft.Extensions.Hosting` compatível, dependências transitivas, qualquer nativo e NuGet audit: ADR 0008 (M2-1).

**Decisões em aberto (para o autor)**

1. `meio_comunicacao` (6 valores) fica fora de filtros e agrupamentos? `servico` e `prefeitura_operacional` não estão em `agrupar_por`: é intencional?
2. `sem_valor`, `total_de_grupos` e `linhas_sem_valor` (§7 e §8): entram?
3. Reconciliação em `tempo_ate_finalizar`: devolver também `n_canceladas`, para a soma bater com o total filtrado?
4. Dias negativos (não ocorrem no arquivo real): contar à parte, ignorar ou falhar?
5. Arredondamento da média (1 casa): `AwayFromZero` ou outro? Não afeta o valor de referência (18,636 vira 18,6).
6. Strings vazias nos filtros: tratar como ausentes ou como erro?
7. Valores mais próximos: métrica (distância de edição sobre a Chave) e limite de 5.
8. Textos de `limitacoes`: os números (47,65%) e as datas escritos no código ou lidos do banco?
9. Suprimir contagens muito pequenas (por exemplo, menores que um mínimo `k`)?
10. Validar o esquema do `.db` na partida e falhar com código `1`?

## 16. Referências

- SDK C# do MCP, documentação v2 (consultada em 2026-11-12): [getting-started](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/getting-started.html), [tools](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/tools/tools.html), [transports](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html).
- [Spec do Marco 1](spec-m1-ingestao.md), [dados](DATA.md), [ADR 0002](decisions/0002-privacidade-sem-enderecos.md), [ADR 0003](decisions/0003-nao-deduplicar-linhas.md), [ADR 0007](decisions/0007-usar-microsoft-data-sqlite.md).
