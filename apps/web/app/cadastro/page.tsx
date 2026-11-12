export default function RegisterPage(){
  return <main className="shell page"><h2>Criar conta</h2>
    <form className="form" action="/api/auth/register" method="post">
      <label>Nome<input name="name" required autoComplete="name"/></label>
      <label>E-mail<input name="email" type="email" required autoComplete="email"/></label>
      <label>Senha<input name="password" type="password" minLength={8} required autoComplete="new-password"/></label>
      <button className="btn primary" type="submit">Criar conta</button>
    </form>
  </main>;
}
