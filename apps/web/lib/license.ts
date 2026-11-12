import {createPrivateKey,randomBytes,sign} from "node:crypto";

const ALPHABET="ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

function block(length=4){
  const bytes=randomBytes(length);
  return Array.from(bytes,b=>ALPHABET[b%ALPHABET.length]).join("");
}

export function generateLicenseSerial(){
  return `OMS-MS-A5-${block()}-${block()}-${block()}`;
}

export function signEntitlement(payload:object){
  const rawKey=process.env.LICENSE_SIGNING_PRIVATE_KEY;
  if(!rawKey) throw new Error("LICENSE_SIGNING_PRIVATE_KEY is missing.");
  const body=Buffer.from(JSON.stringify(payload)).toString("base64url");
  const privateKey=createPrivateKey(rawKey.replace(/\\n/g,"\n"));
  const signature=sign(null,Buffer.from(body),privateKey).toString("base64url");
  return `${body}.${signature}`;
}
