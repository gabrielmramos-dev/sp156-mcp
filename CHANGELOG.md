# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).
O projeto ainda não tem versões publicadas.

## [Não lançado]

### Adicionado
- Estrutura inicial da solução (`Sp156.Ingest` e testes).
- Fixture fictícia do SP156 para testes.
- Documentação: spec do Marco 1, notas sobre os dados e ADRs.
- ADR 0005: escolha do Sylvan.Data.Excel para ler o XLSX, com medições do spike (`tools/spike-xlsx/`, fora da solução).
- Leitor: mapeamento de colunas pelo nome do cabeçalho (NFC, comparação exata), com erros claros e sem eco de conteúdo do arquivo.
- Leitura interna das linhas da aba (`LerLinhasBrutas`): só as 10 colunas mapeadas, valores sem converter, linhas em branco ignoradas e linhas curtas completadas com nulo.
- `Ler` devolve `Pedido`: serial do Excel vira `DateOnly`, `''` vira nulo nas colunas opcionais e linha inválida lança `LinhaInvalidaException` (linha e coluna, sem conteúdo de célula) na enumeração.
- Texto: NFC e trim em todos os textos do `Pedido` (o resto fica como veio) e `TextoNormalizado.Chave` para comparar e buscar (NFC, trim, espaços colapsados, sem acento, minúsculas), sem uso no dado guardado.
- Teste de fumaça com o arquivo real (`SP156_XLSX`): `FactArquivoRealAttribute` marca o teste como `Skipped` sem a variável, e o teste confere os totais da spec e imprime tempo e memória sem afirmá-los.

### Alterado
- Migração de .NET 8 para .NET 10 (`net10.0`, SDK 10.0.115 em `global.json`, mesma política `rollForward: latestFeature`), pois o .NET 8 saiu de suporte em 10/11/2026. Decisão no ADR 0006.
- Dependência `Microsoft.Data.Sqlite` 10.0.15 (MIT; traz o SQLite 3.53.3 nativo via `SQLitePCLRaw` 2.1.13, Apache-2.0). Decisão no ADR 0007.
- `GravadorSqlite`: grava os `Pedido` em uma tabela `pedidos` (10 colunas, sem endereço, 4 `CHECK` nomeados, sem deduplicar). Escreve em `<caminho>.tmp` e renomeia no fim, sem tocar num destino existente e sem deixar `.tmp` ou `-journal` na falha. Teste com o arquivo real confere os totais da spec no banco.
