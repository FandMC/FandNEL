import { Copy, Eye, EyeOff, KeyRound, LoaderCircle } from "lucide-react";
import { FormEvent, ReactNode, useEffect, useState } from "react";
import { DiscordIcon } from "../components/icons";
import { useAuth, useToasts } from "../context/AppContext";
import { disableWebAuthn, getPrivateKey, getWebAuthnStatus, registerWebAuthn } from "../lib/legacyAuth";
import { modifyUsername } from "../lib/legacyPublicApi";

function SettingsCard({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return <section className="account-card"><header><h2>{title}</h2><p>{description}</p></header><div className="account-card-body">{children}</div></section>;
}

export function ProfileSettingsPage() {
  const { user, getAccessToken } = useAuth();
  const { notify } = useToasts();
  const [displayName, setDisplayName] = useState(user?.displayName ?? "");
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState<"idle" | "success" | "error">("idle");

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const nextName = displayName.trim();
    if (nextName === (user?.displayName ?? "").trim()) {
      setStatus("error");
      notify("Please enter a different username.", "error");
      return;
    }
    setSaving(true);
    try {
      await modifyUsername(nextName, await getAccessToken());
      setDisplayName(nextName);
      setStatus("success");
      notify("Username modified successful.", "success");
    } catch {
      setStatus("error");
      notify("Failed to modify username.", "error");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="settings-page">
      <header className="settings-page-header"><h1>Account Settings</h1><p>Manage your profile, personal information, and connected accounts.</p></header>
      <div className="settings-card-list">
        <SettingsCard title="Profile" description="This information will be displayed publicly on your profile.">
          <form className="profile-settings-form" onSubmit={submit}><label><span>Full Name</span><input className={status === "success" ? "settings-input-success" : status === "error" ? "settings-input-error" : ""} type="text" value={displayName} disabled={saving} onChange={(event) => { setDisplayName(event.target.value); setStatus("idle"); }} /></label><div><button type="submit" disabled={saving}>{saving ? "Saving..." : "Save Changes"}</button></div></form>
        </SettingsCard>
        <SettingsCard title="Linked Accounts" description="Connect your social accounts for seamless integration.">
          <div className="linked-account"><span className="linked-account-icon"><DiscordIcon /></span><strong>Discord</strong><button type="button">Connect</button></div>
        </SettingsCard>
      </div>
    </div>
  );
}

export function NotificationSettingsPage() {
  return <div>Page</div>;
}

export function SecuritySettingsPage() {
  const { user } = useAuth();
  const { notify } = useToasts();
  const [password, setPassword] = useState("");
  const [privateKey, setPrivateKey] = useState("");
  const [showPrivateKey, setShowPrivateKey] = useState(false);
  const [retrieving, setRetrieving] = useState(false);
  const [passkeyPending, setPasskeyPending] = useState(false);
  const [statusLoading, setStatusLoading] = useState(true);
  const [passkeyEnabled, setPasskeyEnabled] = useState(false);
  const [error, setError] = useState("");
  const email = localStorage.getItem("UserEmail") || user?.email || "";

  useEffect(() => {
    let active = true;
    if (!email) {
      setStatusLoading(false);
      return () => { active = false; };
    }
    setStatusLoading(true);
    getWebAuthnStatus(email)
      .then((status) => { if (active) setPasskeyEnabled(status.hasWebAuthn && status.isEnabled); })
      .catch(() => { if (active) setPasskeyEnabled(false); })
      .finally(() => { if (active) setStatusLoading(false); });
    return () => { active = false; };
  }, [email]);

  const retrieve = async () => {
    setError("");
    if (!password.trim()) { setError("Please enter your password"); return; }
    setRetrieving(true);
    try {
      setPrivateKey(await getPrivateKey(email, password));
      notify("Private key retrieved successfully.", "success");
    } catch (retrieveError) {
      setError(retrieveError instanceof Error ? retrieveError.message : "Failed to retrieve private key. Please try again.");
    } finally {
      setRetrieving(false);
    }
  };
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(privateKey);
      notify("Private key copied to clipboard.", "success");
    } catch {
      notify("Failed to copy private key.", "error");
    }
  };
  const enablePasskey = async () => {
    if (!privateKey) {
      notify("Please retrieve your private key first.", "error");
      return;
    }
    setPasskeyPending(true);
    try {
      await registerWebAuthn(email);
      setPasskeyEnabled(true);
      notify("WebAuthn enabled successfully.", "success");
    } catch (enableError) {
      notify(enableError instanceof Error ? enableError.message || "Failed to enable WebAuthn." : "Failed to enable WebAuthn.", "error");
    } finally {
      setPasskeyPending(false);
    }
  };
  const disablePasskey = async () => {
    setPasskeyPending(true);
    try {
      await disableWebAuthn(email);
      setPasskeyEnabled(false);
      notify("WebAuthn disabled successfully.", "success");
    } catch (disableError) {
      notify(disableError instanceof Error ? disableError.message || "Failed to disable WebAuthn." : "Failed to disable WebAuthn.", "error");
    } finally {
      setPasskeyPending(false);
    }
  };

  return (
    <div className="settings-page">
      <header className="settings-page-header"><h1>Security &amp; Privacy Settings</h1><p>Manage your private key and other authentication settings.</p></header>
      <div className="settings-card-list">
        <SettingsCard title="Private Key" description="Retrieve and manage your private key securely.">
          <div className="security-fields"><label><span>Password</span><input className={error ? "settings-input-error" : privateKey ? "settings-input-success" : ""} type="password" placeholder="Enter your password" value={password} disabled={retrieving} onChange={(event) => { setPassword(event.target.value); setError(""); }} />{error ? <small className="settings-field-error">{error}</small> : null}</label>{privateKey ? <label><span>Private Key</span><div className="private-key-field"><input type={showPrivateKey ? "text" : "password"} readOnly value={privateKey} /><button type="button" aria-label={showPrivateKey ? "Hide private key" : "Show private key"} onClick={() => setShowPrivateKey((value) => !value)}>{showPrivateKey ? <EyeOff /> : <Eye />}</button><button type="button" aria-label="Copy private key" onClick={copy}><Copy /></button></div></label> : null}<div className="security-actions"><button type="button" disabled={retrieving} onClick={retrieve}>{retrieving ? <><LoaderCircle className="spin" /> Retrieving...</> : "Get Private Key"}</button></div></div>
        </SettingsCard>
        <SettingsCard title="WebAuthn Authentication" description="Enable Passkey for quick and secure access.">
          <div className="passkey-row"><span className="passkey-icon"><KeyRound /></span><div><strong>Passkey</strong><small>{statusLoading ? "Checking status..." : passkeyEnabled ? "Enabled" : "Disabled"}</small></div>{statusLoading ? <LoaderCircle className="spin passkey-status-spinner" /> : passkeyEnabled ? <button className="passkey-disable" type="button" disabled={passkeyPending} onClick={disablePasskey}>{passkeyPending ? "Disabling..." : "Disable"}</button> : <span className="passkey-action"><button type="button" disabled={passkeyPending || !privateKey} onClick={enablePasskey}>{passkeyPending ? "Enabling..." : "Enable"}</button>{!privateKey ? <span className="passkey-tooltip" role="tooltip">Please retrieve your private key first</span> : null}</span>}</div>
        </SettingsCard>
      </div>
    </div>
  );
}
