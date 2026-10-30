# Identidade no GitHub

Para quem publica com o `rb` e precisa que o commit e o release apareçam na própria conta.

O `rb publish all` e o `rb release` leem a conta **ativa** do `gh` e assinam o commit com ela. O `user.name` e o `user.email` gravados no Git da máquina ficam de fora desses commits. O destino do push continua sendo o `origin` do repositório.

Cada publicação mostra a conta escolhida, neste formato:

```text
Conta do publish: login (Nome <id+login@users.noreply.github.com>)
```

Se essa linha for a conta de outra pessoa, interrompa e troque a conta antes de confirmar.

## Conferir a conta

Na pasta do repositório:

```bat
gh auth status
gh api user --jq "{login:.login,name:.name,id:.id}"
```

A conta com `Active account: true` é a que o publish usa.

| Campo | Uso no commit |
|-------|----------------|
| `login` | Conta ligada ao commit no GitHub. |
| `name` | Nome escrito no commit. Com `name` vazio, o `rb` usa o `login`. |
| `id` | Entra no e-mail `{id}+{login}@users.noreply.github.com`, que o GitHub associa a essa conta. |

Neste repositório, além do dono `alpha6678`, a conta com permissão de escrita é **Nickallafe** (id `202808741`). O perfil está sem nome. Enquanto `name` vier vazio, o commit sai com o login `Nickallafe`. Para sair com o nome escolhido no perfil, preencha o nome em [github.com/settings/profile](https://github.com/settings/profile) e rode de novo o `gh api user`. O e-mail que o `rb` grava para essa conta é:

```text
202808741+Nickallafe@users.noreply.github.com
```

## Ajustar a conta ativa

Quando `gh auth status` mostrar outra pessoa:

```bat
gh auth switch
```

Se a conta ainda não estiver no `gh` desta máquina:

```bat
gh auth login
```

Entre com a conta que deve aparecer na linha do tempo. Confirme outra vez:

```bat
gh auth status
gh api user --jq "{login:.login,name:.name,id:.id}"
```

`login` precisa ser o seu. `name` precisa ser o nome que você quer escrito no commit. Se o terminal tiver a variável `GH_TOKEN`, ela vale no lugar da conta ativa: apague essa variável na sessão ou aponte para um token da sua conta.

## Escrita no repositório

A conta precisa conseguir enviar commits para `alpha6678/BananaSuisa`. **Nickallafe** já tem papel de escrita. Um push com `403` significa que a conta ativa é outra: volte ao `gh auth status`.

## Commits feitos à mão

`git commit` fora do `rb` continua usando o Git da máquina. Um e-mail de outra conta faz o GitHub atribuir esse commit a essa conta. Para os commits manuais saírem na mesma conta do `gh`, use o `id` e o `login` do comando acima:

```bat
git config --global user.name "Seu Nome"
git config --global user.email "SEU_ID+seu-login@users.noreply.github.com"
```

Conferir de qual arquivo veio a identidade:

```bat
git config --show-origin user.name
git config --show-origin user.email
```

O e-mail mostrado tem de ser o da sua conta (`id+login@users.noreply.github.com`). Um arquivo de configuração copiado de outro computador, com o e-mail de outra pessoa, faz os commits manuais saírem no nome dela. O `rb publish` e o `rb release` ignoram esse arquivo e usam o `gh`.

## Depois de publicar

Abra o commit no GitHub. A conta ligada ao autor é o `login` do `gh auth status`. O nome ao lado do commit é o `name` do perfil; com o perfil sem nome, aparece o `login`.
