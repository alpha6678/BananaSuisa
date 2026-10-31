# Identidade no GitHub

Repositórios em `Desenvolvimento/projetos/pessoais` só podem ser commitados com **dionerdfrg3@gmail.com** (Dioner Frigi, GitHub `alpha6678`).

Isso vale para `git commit` e para `rb publish` / `rb release`. Outra conta logada no `gh`, inclusive a conta ativa, não assina esses commits.

## Onde está gravado

`~/.gitconfig` inclui `~/.gitconfig-pessoal` quando o repositório está nessa pasta. Esse arquivo define:

| Campo | Valor |
|-------|--------|
| `user.name` | Dioner Frigi |
| `user.email` | `dionerdfrg3@gmail.com` |
| `credential.https://github.com.username` | `alpha6678` |
| `core.hooksPath` | `~/.githooks-pessoal` |

O hook recusa o commit se o autor ou o committer não for `dionerdfrg3@gmail.com`.

Na pasta do repositório:

```bat
git config --show-origin user.name
git config --show-origin user.email
git config --show-origin credential.https://github.com.username
git config --show-origin core.hooksPath
```

Os quatro precisam apontar para a configuração pessoal. Um `user.email` em `.git/config` de outro endereço faz o hook recusar o commit.

## Publish

O `rb` lê essa identidade. O commit sai como `Dioner Frigi <dionerdfrg3@gmail.com>`. O token usado no push e no `gh release create` é o da conta `alpha6678`, mesmo que a conta ativa do `gh` seja outra.

Cada publicação mostra:

```text
Conta do publish: alpha6678 (Dioner Frigi <dionerdfrg3@gmail.com>)
```

Se a conta ativa for outra, um aviso aparece antes dessa linha e deixa essa conta de fora. O `rb` não procura outra conta com escrita.

Se `alpha6678` não estiver logada no `gh`, ou não tiver escrita no `origin`, o publish para antes de alterar versões. Entre com essa conta:

```bat
gh auth login
gh auth status
```

Não é preciso torná-la a conta ativa.

Se o terminal tiver `GH_TOKEN` ou `GITHUB_TOKEN` de outra conta, apague a variável na sessão. Com esse token definido, o `rb` não troca para `alpha6678`.

## Commits feitos à mão

`git commit` usa o mesmo e-mail. Não altere o `user.email` global: a pasta profissional continua com a conta dela, e a pasta pessoal entra pelo `includeIf` do `~/.gitconfig`.
