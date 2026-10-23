---
status: accepted
date: 2026-10-23
---

# Não deduplicar linhas do SP156

## Contexto e problema

No 1º trimestre de 2025, 47,65% das linhas reais são duplicatas exatas de outra. Os dados não têm ID de protocolo, então linhas idênticas são indistinguíveis. Exemplo: 3.368 linhas iguais de "Triagem - Agendamento Atualização CadÚnico" no mesmo dia.

## Opções consideradas

* `DISTINCT` na ingestão
* Manter todas as linhas e tratar a linha como unidade de contagem

## Resultado da decisão

Manter todas as linhas. Sem ID, não há como saber se duas linhas iguais são o mesmo pedido ou pedidos distintos; deduplicar apagaria dados válidos. Toda resposta fala em "linhas/pedidos registrados", nunca em "pessoas" ou "problemas únicos".

### Consequências

* Bom: contagens batem com a fonte.
* Ruim: a métrica não mede problemas distintos. Isso deve constar nas respostas.

### Confirmação

Teste: a fixture tem 30 linhas reais com 3 duplicatas, e o leitor devolve 30.
