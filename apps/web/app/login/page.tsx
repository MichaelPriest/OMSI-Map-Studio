export default function LoginPage(){
  return <main className="shell page"><h2>Entrar</h2>
    <form className="form" action="/api/auth/login" method="post">
      <label>E-mail<input name="email" type="email" required autoComplete="email"/></label>
      <label>Senha<input name="password" type="password" required autoComplete="current-password"/></label>
      <button className="btn primary" type="submit">Entrar</button>
    </form><p className="muted">Ainda não possui conta? <a href="/cadastro">Criar conta</a>.</p>
  </main>;
}
