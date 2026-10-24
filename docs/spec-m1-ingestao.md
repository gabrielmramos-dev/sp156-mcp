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
- Normalização: `Assunto` e `Órgão` → trim, colapsar espaços repetidos, remover acentos (preservar maiúsculas/minúsculas). Ex.: `INTEGRAÇÃO-X` e `INTEGRACAO-X` viram `INTEGRACAO-X`.

## Contrato
- Tipo `Pedido` em `src/Sp156.Ingest/Pedido.cs`, 10 campos. Não alterar sem avisar.
- `LeitorSolicitacoes.Ler(Stream xlsx) : IEnumerable<Pedido>`: uma linha por vez, sem carregar tudo em memória.

## Cabeçalhos
As colunas são mapeadas pelo **nome do cabeçalho**, não pela posição. A comparação é exata (diferencia maiúsculas de minúsculas e acentos), mas ambos os lados são normalizados para Unicode NFC antes, então um cabeçalho decomposto (NFD) equivale ao composto. O nome da aba (`Solicitações`) segue a mesma regra. Colunas extras são ignoradas. As 5 colunas de endereço nunca entram no mapa.

Erros, todos lançados **na chamada de `Ler`**, antes de devolver qualquer pedido:
- Aba ausente: `InvalidDataException`.
- Falta qualquer uma das 10 colunas obrigatórias: `ColunaObrigatoriaAusenteException`.
- Coluna obrigatória duplicada (mesmo nome após NFC): `InvalidDataException`. Duplicata de coluna extra é ignorada.

As mensagens de erro **nunca ecoam o conteúdo do arquivo**. A de coluna ausente traz só os nomes ausentes, a contagem de cabeçalhos encontrados e, por coluna ausente, cabeçalhos que diferem dela apenas por acento ou caixa.

## Critérios de aceitação (fixture `sp156_fixture.xlsx`)
| Verificação | Esperado |
|---|---|
| Linhas devolvidas | 30 |
| Duplicatas exatas mantidas | 3 (27 distintas) |
| Status Finalizada/Aberto/Em Andamento/Cancelada | 27/1/1/1 |
| Pedidos com DataFinalizacao nula | 3 (2 abertos + 1 Finalizada sem data) |
| Pedidos com MeioComunicacao nulo | 1 |
| Assunto com espaço duplo após normalizar | 0 |
| Órgão `INTEGRACAO-SMGI/SGZ` após normalizar | 11 |
| Tabela SQLite com coluna de endereço/coordenada | nenhuma |
| Distrito nulo | 11 |
| Texto de endereço da fixture ("Fictícia") nos bytes do .db gerado | 0 ocorrências |

## Critério com o arquivo real (opcional, variável de ambiente `SP156_XLSX`)
742.904 linhas; Finalizada 715.139; Aberto 23.504; Cancelada 3.948; Em Andamento 313.
DataFinalizacao nula: 24.308 (23.504 Aberto + 313 Em Andamento + 491 Finalizada); MeioComunicacao nulo: 1.
O teste é ignorado quando a variável não está definida.
