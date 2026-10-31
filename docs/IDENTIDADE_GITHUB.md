# Identidade no GitHub

O `rb publish` e o `rb release` usam a conta do `gh` que o GitHub confirma com escrita no `origin`. O nome é o do perfil dessa conta. Com o perfil sem nome, aparece o `login`. O e-mail gravado é `{id}+{login}@users.noreply.github.com`, que o GitHub liga a essa conta.

Se só uma conta logada nesta máquina tiver escrita, ela é usada direto, sem pergunta. Se houver mais de uma, o `rb` pergunta uma vez no terminal qual assina o commit, e os apps seguintes do mesmo comando reutilizam a escolha.

Quem tem acesso hoje: **alpha6678** e **Nickallafe**.

Cada publicação mostra a conta escolhida:

```text
Conta do publish: login (Nome <id+login@users.noreply.github.com>)
```

Só entram na escolha as contas que o GitHub confirma com escrita no `origin`. A conta ativa do `gh` não é mencionada: ela só é usada se tiver escrita.

Se nenhuma conta logada tiver escrita, o `rb` para antes de alterar versões. Entre com a conta:

```bat
gh auth login
gh auth status
gh api user --jq "{login:.login,name:.name,id:.id}"
```

`GH_TOKEN` ou `GITHUB_TOKEN` vale no lugar das contas do `gh auth`. Com a variável definida, a lista só oferece essa conta. Se o token não tiver escrita, o `rb` para. Apague a variável na sessão ou aponte para um token da conta que deve assinar.

## Commits feitos à mão nesta pasta

`git commit` fora do `rb`, na pasta `Desenvolvimento/projetos/pessoais`, continua com `dionerdfrg3@gmail.com` (Dioner Frigi), vindo de `~/.gitconfig-pessoal`. O hook `~/.githooks-pessoal` aceita esse e-mail e também o noreply do GitHub usado pelo `rb`. Um e-mail de outra conta, como o da pasta profissional, continua recusado.
