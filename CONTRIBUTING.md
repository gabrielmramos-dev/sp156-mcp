# Contribuindo

Projeto individual de aprendizagem e portfólio. Sugestões e correções são bem-vindas, mas as decisões de escopo ficam com o mantenedor.

## Antes de abrir um PR

1. Abra uma issue descrevendo o problema, para alinharmos o escopo.
2. Leia `docs/spec-m1-ingestao.md` e os ADRs em `docs/decisions/`. Decisões ali não são reabertas sem justificativa nova.
3. Escreva o teste primeiro e mostre que ele falha pelo motivo certo.
4. Não altere um teste ou a fixture só para fazê-lo passar.
5. Não adicione dependências sem discutir antes.
6. **Nunca** inclua dados reais do SP156 no PR.

## Checklist

- [ ] `dotnet build` e `dotnet test` passam localmente
- [ ] Nenhuma coluna de endereço ou coordenada é lida ou gravada
- [ ] Nenhum arquivo de dados real foi adicionado
- [ ] Decisões novas relevantes viraram um ADR em `docs/decisions/`
