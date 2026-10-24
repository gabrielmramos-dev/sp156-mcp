# sp156-mcp

Servidor [MCP](https://modelcontextprotocol.io) **não oficial**, em C#, sobre os dados abertos do SP156 (Central de Atendimento da Prefeitura de São Paulo).

> **Status: em construção.** Hoje existe apenas a base de ingestão (Marco 1). **Ainda não há servidor MCP nem ferramentas.** Este README descreve o que o projeto pretende ser e o que já foi verificado.

## O que pretende ser

Permitir que um assistente de IA responda perguntas agregadas sobre pedidos feitos ao SP156 (quantos, onde, quando, tendência), sempre acompanhadas das ressalvas dos dados. Exemplo de pergunta: "quais distritos mais pediram remoção de entulho no 1º trimestre de 2025?".

## Princípios

- **Só agregados.** Endereço, número, CEP e coordenadas **não são mapeados para o modelo nem gravados**. Ver [ADR 0002](docs/decisions/0002-privacidade-sem-enderecos.md).
- **A linha é a unidade, não a "solicitação".** Os dados não têm ID de protocolo e quase metade das linhas é idêntica a outra. Nada é deduplicado. Ver [ADR 0003](docs/decisions/0003-nao-deduplicar-linhas.md).
- **Toda resposta carrega o contexto:** fonte, período, data da extração e avisos (por exemplo, quantas linhas não têm distrito, quantos pedidos seguem abertos).
- **Evidência antes de afirmação.** Nada aqui é "provado": os testes verificam casos concretos, e o que eles não cobrem está descrito em [docs/DATA.md](docs/DATA.md).

## Fonte dos dados

Portal de dados abertos da Prefeitura de São Paulo (dados.prefeitura.sp.gov.br), conjunto de dados do SP156, publicado sob **CC0 1.0 Universal**. Este projeto **não é afiliado nem endossado** pela Prefeitura de São Paulo.

Detalhes, limitações e divergências entre a documentação oficial e os arquivos reais: [docs/DATA.md](docs/DATA.md).

## Estrutura

```
src/Sp156.Ingest/            leitura do XLSX e gravação em SQLite
tests/Sp156.Ingest.Tests/    testes e fixture fictícia
docs/                        spec, dados, decisões (ADRs), diário de aprendizagem
```

## Desenvolvimento

```bash
dotnet build
dotnet test
```

Teste opcional contra o arquivo real (não versionado; ignorado quando a variável não existe):

```bash
SP156_XLSX=/caminho/arquivo-final-1-trim-25.xlsx dotnet test
```

Dados reais **nunca** entram no git (`.gitignore`). A única planilha versionada é a fixture fictícia em `tests/.../Fixtures/`.

Fluxo de trabalho: spec → testes vermelhos → implementação mínima. Ver [AGENTS.md](AGENTS.md) e [CONTRIBUTING.md](CONTRIBUTING.md).

## Licença

Código sob [MIT](LICENSE). Os dados do SP156 seguem a licença da fonte (CC0 1.0).
