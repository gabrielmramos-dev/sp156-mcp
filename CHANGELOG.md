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
- Leitura interna das linhas da aba (`LerLinhasBrutas`): só as 10 colunas mapeadas, valores sem converter, linhas em branco ignoradas e linhas curtas completadas com nulo. `Ler` só devolve `Pedido` na tarefa 4.
