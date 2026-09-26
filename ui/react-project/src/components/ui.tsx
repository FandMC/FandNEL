import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  LoaderCircle,
  Search,
  X,
  type LucideIcon,
} from "lucide-react";
import { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from "react";
import { Link } from "react-router-dom";

export function Brand({ compact = false, to = "/user-center" }: { compact?: boolean; to?: string }) {
  return (
    <Link className="brand" to={to} aria-label="FandNEL">
      <svg width={compact ? 30 : 35} height={compact ? 30 : 35} viewBox="0 0 200 200" fill="none" aria-hidden="true">
        <path
          d="M139.977 53.454C130.158 56.794 125.43 62.784 122.21 72.112c-.831 2.492-2.91 5.248-5.247 7.103l8.052 8.799-25.56-18.234-70.549-50.249s5.091 33.711 6.857 46.115c1.247 8.745 3.377 12.667 10.131 16.644l14.339 7.738-6.91-3.657 31.898 17.756-.208.478-34.339-16.22c1.818 6.361 5.35 18.605 6.857 24.012 1.61 5.83 3.429 7.95 8.988 10.018l10.234 3.816 6.338-2.544-8.052 5.46-40.262 52.103c26.754-25.336 49.405-34.347 65.977-41.715 21.144-9.329 33.871-15.318 42.184-36.839 5.922-15.106 10.545-34.452 16.417-41.927l12.52-16.324s-25.924 6.995-31.898 9.01Z"
          fill="#086DA4"
        />
      </svg>
      <span>FandNEL</span>
    </Link>
  );
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  tone?: "primary" | "secondary" | "quiet" | "danger";
  icon?: LucideIcon;
  loading?: boolean;
}

export function Button({
  children,
  className = "",
  tone = "primary",
  icon: Icon,
  loading,
  disabled,
  ...props
}: ButtonProps) {
  return (
    <button className={`button button-${tone} ${className}`} disabled={disabled || loading} {...props}>
      {loading ? <LoaderCircle className="spin" size={18} /> : Icon ? <Icon size={18} /> : null}
      {children}
    </button>
  );
}

interface FieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  leadingIcon?: LucideIcon;
}

export function Field({ label, error, leadingIcon: Icon, className = "", ...props }: FieldProps) {
  return (
    <label className={`field ${error ? "field-error" : ""} ${className}`}>
      {label ? <span>{label}</span> : null}
      <div className="field-control">
        {Icon ? <Icon size={18} /> : null}
        <input {...props} />
      </div>
      {error ? <small>{error}</small> : null}
    </label>
  );
}

export function SearchField(props: InputHTMLAttributes<HTMLInputElement>) {
  return <Field leadingIcon={Search} type="search" {...props} />;
}

export function PageHeader({
  title,
  description,
  backTo,
  onBack,
  actions,
}: {
  title: string;
  description?: string;
  backTo?: string;
  onBack?(): void;
  actions?: ReactNode;
}) {
  return (
    <header className="page-heading">
      <div className="page-heading-copy">
        {onBack ? (
          <button className="back-link" type="button" onClick={onBack} aria-label="Back">
            <ArrowLeft size={19} />
          </button>
        ) : backTo ? (
          <Link className="back-link" to={backTo} aria-label="Back">
            <ArrowLeft size={19} />
          </Link>
        ) : null}
        <div>
          <h1>{title}</h1>
          {description ? <p>{description}</p> : null}
        </div>
      </div>
      {actions ? <div className="page-actions">{actions}</div> : null}
    </header>
  );
}

export function LoadingState({ label = "Loading" }: { label?: string }) {
  return (
    <div className="loading-state" aria-label={label} aria-busy="true">
      <LoaderCircle className="spin" size={26} />
    </div>
  );
}

export function EmptyState({
  icon: Icon = AlertCircle,
  title,
  description,
  action,
}: {
  icon?: LucideIcon;
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className="empty-state">
      <Icon size={28} />
      <h2>{title}</h2>
      {description ? <p>{description}</p> : null}
      {action}
    </div>
  );
}

export function StatusBadge({ tone, children }: { tone: "success" | "warning" | "neutral"; children: ReactNode }) {
  return <span className={`status status-${tone}`}>{children}</span>;
}

export function Notice({ tone = "success", children }: { tone?: "success" | "error"; children: ReactNode }) {
  const Icon = tone === "success" ? CheckCircle2 : AlertCircle;
  return (
    <div className={`notice notice-${tone}`}>
      <Icon size={18} />
      <span>{children}</span>
    </div>
  );
}

export function Modal({ title, children, onClose }: { title: string; children: ReactNode; onClose(): void }) {
  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={onClose}>
      <section className="modal" role="dialog" aria-modal="true" aria-label={title} onMouseDown={(event) => event.stopPropagation()}>
        <header>
          <h2>{title}</h2>
          <button className="icon-button" onClick={onClose} aria-label="Close"><X size={18} /></button>
        </header>
        {children}
      </section>
    </div>
  );
}
