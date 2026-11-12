import "./globals.css";
import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = { title: "OMSI Map Studio", description: "Editor moderno e independente de mapas para OMSI 2." };

export default function RootLayout({children}:Readonly<{children:React.ReactNode}>){
  return <html lang="pt-BR"><body>
    <header className="shell nav">
      <Link className="brand" href="/">OMSI <span>MAP STUDIO</span></Link>
      <nav className="navlinks">
        <Link href="/#recursos">Recursos</Link><Link href="/precos">Assinatura</Link>
        <Link href="/login">Entrar</Link><Link className="btn primary" href="/cadastro">Criar conta</Link>
      </nav>
    </header>
    {children}
    <footer><div className="shell">OMSI Map Studio · Public Alpha · Projeto independente do OMSI NavBR Multiplayer.</div></footer>
  </body></html>;
}
