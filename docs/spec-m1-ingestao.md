# Spec M1: Ingestão do SP156

## Objetivo
Ler o XLSX do SP156 (aba `Solicitações`) em streaming e gravar em SQLite, sem nenhuma coluna de endereço.

## Fora de escopo
Servidor MCP, ferramentas, aba `Satisfação`, GeoSampa, Docker. Não tocar nisso.

## Decisões já tomadas (não reabrir sem avisar)
- Privacidade: NÃO mapear para o modelo nem gravar Logradouro, Número, CEP, Latitude, Longitude (ADR 0002).
- A linha é a unidade. NÃO deduplicar (ADR 0003).
- Linhas totalmente vazias são ignoradas (a aba declara ~1.048.576 linhas, mas só ~742 mil têm dado).
- Datas: `DateOnly` no código; no SQLite, texto `yyyy-MM-dd`.
- `Status` é `string` (a documentação oficial diverge do arquivo real).
- String vazia (`''`) em `Data de Finalização` equivale a nulo (é como o arquivo real marca a ausência).
- `MeioComunicacao` é anulável (1 linha real sem valor).
- `PrefeituraOperacional` e `Distrito` podem ser nulos.
- Texto: todos os textos recebem NFC e trim. O resto é **mantido como veio** (espaço duplo no meio, acento e caixa), por fidelidade à fonte. A normalização mais agressiva do dado guardado foi **adiada por falta de evidência**: a medição no 1º trimestre de 2025 não achou variação de acento ou caixa em nenhuma coluna e só 13 valores com espaço duplo (2 em `Assunto`, 11 em `Serviço`). Outros trimestres devem ser checados antes de qualquer normalização mais agressiva.
- `TextoNormalizado.Chave(string)` (função pura e pública) é a forma comparável para busca e agrupamento no Marco 2: NFC, trim, espaços internos colapsados em um, sem acentos e em minúsculas. **Não é aplicada ao dado guardado** e não há coluna de chave no banco.

## Contrato
- Tipo `Pedido` em `src/Sp156.Ingest/Pedido.cs`, 10 campos. Não alterar sem avisar.
- `LeitorSolicitacoes.Ler(Stream xlsx) : IEnumerable<Pedido>`: uma linha por vez, sem carregar tudo em memória.
  - `Ler` valida aba e cabeçalho **na chamada** e devolve os pedidos **na enumeração**, uma vez só. Os erros de linha (`LinhaInvalidaException`, com `Linha` e `Coluna`) saem na enumeração, quando a linha ruim é lida, e a linha é o número que o Excel mostra.
  - A camada interna `LerLinhasBrutas` percorre a aba e devolve `LinhaBruta` (número da linha e só as 10 colunas mapeadas, valores sem converter), ignorando linhas em branco. `Ler` converte cada `LinhaBruta` em `Pedido`.
  - Linha em branco = as 10 colunas obrigatórias nulas, vazias ou só com espaços. Linha curta (menos células que o cabeçalho) completa as ausentes com nulo.
  - Conversão: `Data de Abertura` e `Data de Finalização` numéricas são o serial do Excel e viram `DateOnly` sem hora (`DateTime.FromOADate`). `Data de Finalização` nula ou `''` vira `null`; `Meio de Comunicação`, `Prefeitura Operacional` e `Distrito` nulos, `''` ou vazios depois do trim viram `null`.
  - Linha inválida (exceção com linha e coluna, **sem** conteúdo de célula): `Data de Abertura`, `Tema`, `Assunto`, `Serviço`, `Status` ou `Órgão` nulos, `''` ou vazios depois do trim; texto não vazio (inclusive só espaços) ou serial fora do intervalo numa coluna de data.
  - Texto: NFC e trim; o espaço do meio, o acento e a caixa ficam como vieram. Valor numérico numa coluna de texto vira texto com `CultureInfo.InvariantCulture`.

## Cabeçalhos
As colunas são mapeadas pelo **nome do cabeçalho**, não pela posição. A comparação é exata (diferencia maiúsculas de minúsculas e acentos), mas ambos os lados são normalizados para Unicode NFC antes, então um cabeçalho decomposto (NFD) equivale ao composto. O nome da aba (`Solicitações`) segue a mesma regra. Colunas extras são ignoradas. As 5 colunas de endereço nunca entram no mapa.

Erros, todos lançados **na chamada de `Ler`**, antes de devolver qualquer pedido:
- Aba ausente: `InvalidDataException`.
- Falta qualquer uma das 10 colunas obrigatórias: `ColunaObrigatoriaAusenteException`.
- Coluna obrigatória duplicada (mesmo nome após NFC): `InvalidDataException`. Duplicata de coluna extra é ignorada.

As mensagens de erro **nunca ecoam o conteúdo do arquivo**. A de coluna ausente traz só os nomes ausentes, a contagem de cabeçalhos encontrados e, por coluna ausente, cabeçalhos que diferem dela apenas por acento ou caixa.

## Esquema SQLite
`GravadorSqlite.Gravar(IEnumerable<Pedido> pedidos, string caminho)` grava os pedidos em uma tabela só e devolve a quantidade de linhas gravadas. Uma linha no banco por pedido lido: sem deduplicação (ADR 0003), sem `PRIMARY KEY` e sem índices. O `rowid` implícito do SQLite preserva a ordem de inserção. As colunas de endereço e coordenadas não existem (ADR 0002).

```sql
CREATE TABLE pedidos (
    data_abertura          TEXT NOT NULL,
    data_finalizacao       TEXT,
    meio_comunicacao       TEXT,
    tema                   TEXT NOT NULL,
    assunto                TEXT NOT NULL,
    servico                TEXT NOT NULL,
    status                 TEXT NOT NULL,
    orgao                  TEXT NOT NULL,
    prefeitura_operacional TEXT,
    distrito               TEXT,
    CONSTRAINT ck_data_abertura_formato CHECK (data_abertura GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    CONSTRAINT ck_data_finalizacao_formato CHECK (data_finalizacao IS NULL OR data_finalizacao GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    CONSTRAINT ck_obrigatorios_nao_vazios CHECK (tema <> '' AND assunto <> '' AND servico <> '' AND status <> '' AND orgao <> ''),
    CONSTRAINT ck_opcionais_nulo_ou_preenchido CHECK ((meio_comunicacao IS NULL OR meio_comunicacao <> '') AND (prefeitura_operacional IS NULL OR prefeitura_operacional <> '') AND (distrito IS NULL OR distrito <> ''))
);
```

- **Datas:** `TEXT` no formato `yyyy-MM-dd`, com cultura invariante (ex.: `2025-01-06`).
- **Ausência:** campo opcional ausente é `NULL` de verdade, nunca `''`.
- **O que cada restrição protege:**
  - `ck_data_abertura_formato` e `ck_data_finalizacao_formato`: a data é gravada sempre como `yyyy-MM-dd`, o que mantém a ordenação e a comparação por texto corretas. A de finalização também aceita `NULL`.
  - `ck_obrigatorios_nao_vazios`: os cinco textos obrigatórios nunca ficam vazios (o `NOT NULL` não pega `''`).
  - `ck_opcionais_nulo_ou_preenchido`: os três opcionais são `NULL` ou têm texto, nunca `''`, para que `IS NULL` conte tudo o que está ausente.
- **Escrita segura:** grava em `<caminho>.tmp` e, no fim, renomeia com `File.Move(tmp, destino, overwrite: false)`. Se o destino já existir, lança `IOException` sem tocá-lo. Se a gravação falhar, o `.tmp` é apagado, e não ficam arquivos `-journal` ou `-wal` ao lado. Um `.tmp` deixado por uma gravação anterior também faz a gravação lançar `IOException`, sem ser tocado; a mensagem diz o caminho do `.tmp` e que ele pode ser apagado se nenhuma outra gravação estiver em andamento.
- **Conexão:** `Pooling=False`, uma transação só e um comando parametrizado reaproveitado.
- **Erros:** as mensagens de erro de gravação não trazem o conteúdo das células (ADR 0002): as do SQLite citam só o nome da restrição ou da coluna.

## Critérios de aceitação (fixture `sp156_fixture.xlsx`)
| Verificação | Esperado |
|---|---|
| Linhas devolvidas | 30 |
| Duplicatas exatas mantidas | 3 (27 distintas) |
| Status Finalizada/Aberto/Em Andamento/Cancelada | 27/1/1/1 |
| Pedidos com DataFinalizacao nula | 3 (2 abertos + 1 Finalizada sem data) |
| Pedidos com MeioComunicacao nulo | 1 |
| Assunto `Reparos em  Asfalto, Pontes, Viadutos e Túneis` (espaço duplo no meio) | mantido sem alteração |
| `TextoNormalizado.Chave` de `INTEGRAÇÃO-SMGI/SGZ` e de `integracao-smgi/sgz` | iguais |
| Tabela SQLite com coluna de endereço/coordenada | nenhuma |
| Distrito nulo | 11 |
| Texto de endereço da fixture ("Fictícia") nos bytes do .db gerado | 0 ocorrências |

## Critério com o arquivo real (opcional, variável de ambiente `SP156_XLSX`)
742.904 linhas; Finalizada 715.139; Aberto 23.504; Cancelada 3.948; Em Andamento 313.
DataFinalizacao nula: 24.308 (23.504 Aberto + 313 Em Andamento + 491 Finalizada); MeioComunicacao nulo: 1.
O teste é ignorado quando a variável não está definida.

Privacidade por amostra (ADR 0002): lê do XLSX só as colunas de endereço e coordenadas, monta uma amostra determinística (um valor a cada 4.000, não vazio, com 7 ou mais caracteres, até 200 distintos por coluna) e afirma que nenhum valor aparece, como bytes UTF-8, no `.db` gerado. É uma **amostra** de 1 trimestre: não é uma verificação exaustiva e não diz nada sobre o que a biblioteca de leitura decodifica em memória. Detalhes e limites no [ADR 0002](decisions/0002-privacidade-sem-enderecos.md).
