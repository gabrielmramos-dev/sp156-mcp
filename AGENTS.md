# AGENTS.md

Instruções para agentes de código (Claude Code, Codex, Cursor etc.). Fonte única de verdade: o `CLAUDE.md` apenas importa este arquivo.

## Projeto

Servidor MCP não oficial, em C#/.NET, sobre os dados abertos do SP156 (Prefeitura de São Paulo). **Estado atual: Marco 1 (ingestão XLSX → SQLite). Não existe servidor MCP ainda.**

Leia antes de agir: `docs/spec-m1-ingestao.md`, `docs/DATA.md`, `docs/decisions/`.

## Comandos

```bash
dotnet build
dotnet test
SP156_XLSX=/caminho/arquivo.xlsx dotnet test   # opcional, contra o arquivo real
```

## Regras inegociáveis

- **Privacidade:** nunca mapear para o modelo nem gravar Logradouro, Número, CEP, Latitude, Longitude (ADR 0002).
- **Não deduplicar linhas** (ADR 0003).
- **Nunca** commitar dados reais, bancos (`*.db`, `*.sqlite`) ou planilhas fora de `tests/**/Fixtures/`.
- **Nunca alterar um teste ou a fixture para fazê-lo passar.** Se a spec ou o teste parecer errado, pare e avise.
- **Não adicionar pacotes** sem propor antes (comparação, versão atual, manutenção).
- **Não inventar números.** Toda métrica citada deve vir de uma execução real.
- Não implementar nada fora do escopo da spec vigente.

## Fluxo (SDD + TDD)

1. Propor plano em tarefas pequenas, cada uma com o teste que a prova. Esperar aprovação.
2. Escrever o teste, rodar e mostrar que falha por asserção (não por exceção).
3. Implementar o mínimo para passar. Mostrar a saída real de `dotnet test`.
4. Ao fim de cada tarefa, escrever 5 linhas: o que mudou, por quê, alternativa descartada e o que o autor precisa conseguir explicar sozinho. Parar e esperar.

## Convenções

- C#, `Nullable` habilitado. Campo opcional nos dados → tipo anulável (`string?`, `DateOnly?`).
- Datas como `DateOnly`; no SQLite, texto `yyyy-MM-dd`.
- Leitura em streaming: não carregar o arquivo inteiro em memória.
- Textos de documentação em português do Brasil.

## Definição de pronto

Testes verdes, critérios da spec cumpridos, `CHANGELOG.md` atualizado e, se houve decisão relevante, um ADR novo.
