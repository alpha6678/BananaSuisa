# PowerShell + WinForms (notas)

- Operações longas usam `System.Diagnostics.Process` com leitura incremental de streams e `Application.DoEvents()` no laço principal.
- Para evitar reentrância excessiva, mantenha trabalho pesado fora de handlers síncronos críticos; o padrão atual prioriza simplicidade e responsividade.
