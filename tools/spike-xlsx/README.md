# Spike: leitura de XLSX grande

Código descartável que mediu o pico de memória e o tempo de leitura do XLSX real com Sylvan.Data.Excel e ExcelDataReader. Resultados e limites no [ADR 0005](../../docs/decisions/0005-biblioteca-de-leitura-xlsx.md).

Fica **fora da solução** (`Sp156.sln`) e do CI. Não é código de produção.

```bash
SP156_XLSX=/caminho/arquivo.xlsx dotnet run -c Release --project tools/spike-xlsx -- sylvan
SP156_XLSX=/caminho/arquivo.xlsx dotnet run -c Release --project tools/spike-xlsx -- edr
```

Cada execução deve ser um processo separado, porque o pico de working set é do processo.
