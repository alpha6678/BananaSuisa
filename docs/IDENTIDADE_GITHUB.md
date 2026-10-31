# Identidade no GitHub

O `rb publish` e o `rb release` abrem o diálogo do Windows em qualquer máquina. A lista mostra as contas do `gh` já logadas que têm escrita no `origin`. A pessoa escolhe qual assina o commit. O nome é o do perfil dessa conta. Com o perfil sem nome, aparece o `login`. O e-mail gravado é `{id}+{login}@users.noreply.github.com`, que o GitHub liga a essa conta.

Quem tem acesso hoje: **alpha6678** e **Nickallafe**.

Cada publicação mostra a conta escolhida:

```text
Conta do publish: login (Nome <id+login@users.noreply.github.com>)
```

A conta ativa do `gh` vem marcada no diálogo. Escolher outra deixa um aviso antes dessa linha. Cancelar o diálogo interrompe o publish.

Se nenhuma conta logada tiver escrita, o `rb` para antes de alterar versões. Entre com a conta:

```bat
gh auth login
gh auth status
gh api user --jq "{login:.login,name:.name,id:.id}"
```

`GH_TOKEN` ou `GITHUB_TOKEN` vale no lugar das contas do `gh auth`. Com a variável definida, o diálogo só oferece essa conta. Se o token não tiver escrita, o `rb` para. Apague a variável na sessão ou aponte para um token da conta que deve assinar.

## Commits feitos à mão nesta pasta

`git commit` fora do `rb`, na pasta `Desenvolvimento/projetos/pessoais`, continua com `dionerdfrg3@gmail.com` (Dioner Frigi), vindo de `~/.gitconfig-pessoal`. O hook `~/.githooks-pessoal` aceita esse e-mail e também o noreply do GitHub usado pelo `rb`. Um e-mail de outra conta, como o da pasta profissional, continua recusado.
