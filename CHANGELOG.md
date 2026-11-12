# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).
A versão 0.1.0 existe como tag local; nada foi publicado.

## [Não lançado]

### Alterado
- Documentação (`docs/DATA.md`): registro do arquivo medido (nome, tamanho, SHA-256 e data de modificação local), correção sobre o período coberto (a página da Transparência está desatualizada em relação ao portal de dados abertos) e novos itens em "ainda não verificado" (CSV, retificação de 2025, só o 1º TRI 2025 medido).

## [0.1.0] - 2026-11-12

Marco 1: ingestão do SP156 para SQLite. Ainda não há servidor MCP.

### Adicionado
- `Sp156.Ingest`: leitura do XLSX (aba `Solicitações`) em streaming com Sylvan.Data.Excel, mapeando as colunas pelo nome do cabeçalho (NFC, comparação exata) e devolvendo `Pedido` com as 10 colunas usadas. Linhas em branco são ignoradas, linhas curtas completam as células ausentes com nulo, o serial do Excel vira `DateOnly` e `''` vira nulo nas colunas opcionais. Nenhuma coluna de endereço é mapeada (ADR 0002) e nada é deduplicado (ADR 0003).
- Erros de aba e cabeçalho na chamada de `Ler`; erros de linha (`LinhaInvalidaException`, com linha e coluna) na enumeração. As mensagens nunca trazem conteúdo de célula.
- Texto: NFC e trim em todos os textos do `Pedido` (o resto fica como veio) e `TextoNormalizado.Chave` para comparar e buscar (NFC, trim, espaços colapsados, sem acento, minúsculas), sem uso no dado guardado.
- `GravadorSqlite`: tabela `pedidos` (10 colunas, sem endereço, 4 `CHECK` nomeados, sem deduplicar). Escreve em `<caminho>.tmp` e renomeia no fim, sem tocar num destino existente e sem deixar `.tmp` ou `-journal` na falha.
- `Sp156.Cli`: comando `ingerir <arquivo.xlsx> <saida.db>`, com parsing manual e sem pacote novo. Códigos de saída: 0 sucesso, 1 erro, 2 uso incorreto. Checa o destino antes de abrir o XLSX e trata arquivo corrompido ou que não é XLSX sem imprimir a mensagem da biblioteca (o `Sp156.Ingest` marca internamente as `InvalidDataException` que ele mesmo lança).
- Testes: fixture fictícia; teste de fumaça com o arquivo real (`SP156_XLSX`, `Skipped` sem a variável) que confere os totais da spec; privacidade nos bytes do `.db` da fixture; privacidade por **amostra** no arquivo real do 1º trimestre de 2025 (não é prova exaustiva; ver ADR 0002); testes unitários do buscador de bytes (fronteira entre blocos, acentos).
- Link do conjunto de dados do SP156 no README e no `docs/DATA.md`, e a limitação de cobertura: os dados só registram solicitações feitas pela população, então o volume de pedidos não mede os serviços executados nem os problemas da cidade (fonte: página Dados Abertos – SP156 da Prefeitura).
- Documentação: spec do Marco 1, notas sobre os dados (`docs/DATA.md`) e ADRs 0001 a 0007. `tools/spike-xlsx/` guarda a medição das bibliotecas de XLSX, fora da solução.

### Alterado
- Migração de .NET 8 para .NET 10 (`net10.0`, SDK 10.0.115 em `global.json`, mesma política `rollForward: latestFeature`), pois o .NET 8 saiu de suporte em 10/11/2026. Decisão no ADR 0006.
- Dependências: Sylvan.Data.Excel 0.5.8 (ADR 0005) e `Microsoft.Data.Sqlite` 10.0.15 (MIT; traz o SQLite 3.53.3 nativo via `SQLitePCLRaw` 2.1.13, Apache-2.0; ADR 0007).
- `.gitignore` cobre `*.sqlite3` e os arquivos `-journal`, `-wal` e `-shm` do SQLite.

### Limitações conhecidas
- Verificado em um único arquivo (1º trimestre de 2025).
- Nada se afirma sobre o que a biblioteca de leitura decodifica em memória.
- A amostra de privacidade cobre `Número` quase nada (1 valor); a proteção dessa coluna vem do esquema.
