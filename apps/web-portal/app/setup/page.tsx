'use client'

import { useState } from 'react'
import {
  Card,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
} from '../../components/ui'

// ─── Types ───────────────────────────────────────────────────────────────────

interface InvoicingRules {
  billingDay: number
  paymentTermsDays: number
  vatRate: number
  latePenaltyRate: number
  autoGenerate: boolean
  includeDamages: boolean
  includeViolations: boolean
  invoicePrefix: string
  currency: string
}

interface CompanyProfile {
  companyName: string
  crNo: string
  vatNo: string
  address: string
  phone: string
  email: string
  logoUrl: string
}

interface NotificationRules {
  sendInvoiceEmail: boolean
  sendPaymentReceipt: boolean
  overdueReminderDays: number
  escalationAfterDays: number
}

// ─── Defaults ────────────────────────────────────────────────────────────────

const DEFAULT_INVOICING: InvoicingRules = {
  billingDay: 1,
  paymentTermsDays: 10,
  vatRate: 15,
  latePenaltyRate: 2,
  autoGenerate: false,
  includeDamages: true,
  includeViolations: true,
  invoicePrefix: 'INV-',
  currency: 'SAR',
}

const DEFAULT_COMPANY: CompanyProfile = {
  companyName: 'Auto Lead Company',
  crNo: '1010012345',
  vatNo: '300123456789003',
  address: 'Riyadh, Kingdom of Saudi Arabia',
  phone: '+966 11 234 5678',
  email: 'info@autolead.com.sa',
  logoUrl: '',
}

const DEFAULT_NOTIFICATIONS: NotificationRules = {
  sendInvoiceEmail: true,
  sendPaymentReceipt: true,
  overdueReminderDays: 7,
  escalationAfterDays: 30,
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

type Tab = 'invoicing' | 'company' | 'notifications'

const TAB_LABELS: Record<Tab, string> = {
  invoicing: 'Invoicing Rules',
  company: 'Company Profile',
  notifications: 'Notification Rules',
}

function FieldLabel({ label, htmlFor }: { label: string; htmlFor?: string }) {
  return (
    <label htmlFor={htmlFor} className="mb-1 block text-sm font-medium text-slate-700">
      {label}
    </label>
  )
}

function TextInput({ id, value, onChange, placeholder }: { id: string; value: string; onChange: (v: string) => void; placeholder?: string }) {
  return (
    <input
      id={id}
      type="text"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
    />
  )
}

function NumberInput({ id, value, onChange, min, max, step }: { id: string; value: number; onChange: (v: number) => void; min?: number; max?: number; step?: number }) {
  return (
    <input
      id={id}
      type="number"
      value={value}
      onChange={(e) => onChange(Number(e.target.value))}
      min={min}
      max={max}
      step={step}
      className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
    />
  )
}

function Toggle({ id, checked, onChange, label }: { id: string; checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <label htmlFor={id} className="flex cursor-pointer items-center gap-3">
      <div className="relative">
        <input
          id={id}
          type="checkbox"
          checked={checked}
          onChange={(e) => onChange(e.target.checked)}
          className="sr-only"
        />
        <div className={`h-6 w-11 rounded-full transition ${checked ? 'bg-brand-600' : 'bg-slate-300'}`} />
        <div className={`absolute left-0.5 top-0.5 h-5 w-5 rounded-full bg-white shadow transition ${checked ? 'translate-x-5' : ''}`} />
      </div>
      <span className="text-sm text-slate-700">{label}</span>
    </label>
  )
}

// ─── Page component ──────────────────────────────────────────────────────────

export default function SetupPage() {
  const [activeTab, setActiveTab] = useState<Tab>('invoicing')
  const [invoicing, setInvoicing] = useState<InvoicingRules>({ ...DEFAULT_INVOICING })
  const [company, setCompany] = useState<CompanyProfile>({ ...DEFAULT_COMPANY })
  const [notifications, setNotifications] = useState<NotificationRules>({ ...DEFAULT_NOTIFICATIONS })
  const [saved, setSaved] = useState(false)

  function handleSave() {
    setSaved(true)
    setTimeout(() => setSaved(false), 3000)
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Setup"
        subtitle="Configure invoicing rules, company profile and notification settings."
        action={
          <PrimaryButton onClick={handleSave}>
            {saved ? 'Settings Saved' : 'Save Settings'}
          </PrimaryButton>
        }
      />

      {/* ── Success message ─────────────────────────────────────────────────── */}
      {saved && (
        <div className="rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-800">
          Settings saved successfully.
        </div>
      )}

      {/* ── Tabs ────────────────────────────────────────────────────────────── */}
      <div className="flex gap-1 border-b border-slate-200">
        {(Object.keys(TAB_LABELS) as Tab[]).map((tab) => (
          <button
            key={tab}
            type="button"
            onClick={() => setActiveTab(tab)}
            className={`px-4 py-2.5 text-sm font-medium transition ${
              activeTab === tab
                ? 'border-b-2 border-brand-600 text-brand-700'
                : 'text-slate-500 hover:text-slate-700'
            }`}
          >
            {TAB_LABELS[tab]}
          </button>
        ))}
      </div>

      {/* ── Invoicing Rules ─────────────────────────────────────────────────── */}
      {activeTab === 'invoicing' && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <Card className="p-5">
            <h3 className="mb-4 text-sm font-semibold uppercase tracking-wide text-slate-600">Billing Settings</h3>
            <div className="space-y-4">
              <div>
                <FieldLabel label="Monthly billing day (1-28)" htmlFor="billingDay" />
                <NumberInput id="billingDay" value={invoicing.billingDay} onChange={(v) => setInvoicing({ ...invoicing, billingDay: Math.max(1, Math.min(28, v)) })} min={1} max={28} />
              </div>
              <div>
                <FieldLabel label="Payment terms (days)" htmlFor="paymentTerms" />
                <NumberInput id="paymentTerms" value={invoicing.paymentTermsDays} onChange={(v) => setInvoicing({ ...invoicing, paymentTermsDays: v })} min={1} max={90} />
              </div>
              <div>
                <FieldLabel label="VAT rate (%)" htmlFor="vatRate" />
                <NumberInput id="vatRate" value={invoicing.vatRate} onChange={(v) => setInvoicing({ ...invoicing, vatRate: v })} min={0} max={100} step={0.5} />
              </div>
              <div>
                <FieldLabel label="Late payment penalty rate (%)" htmlFor="penaltyRate" />
                <NumberInput id="penaltyRate" value={invoicing.latePenaltyRate} onChange={(v) => setInvoicing({ ...invoicing, latePenaltyRate: v })} min={0} max={20} step={0.5} />
              </div>
            </div>
          </Card>

          <Card className="p-5">
            <h3 className="mb-4 text-sm font-semibold uppercase tracking-wide text-slate-600">Invoice Options</h3>
            <div className="space-y-4">
              <div>
                <FieldLabel label="Invoice number prefix" htmlFor="invoicePrefix" />
                <TextInput id="invoicePrefix" value={invoicing.invoicePrefix} onChange={(v) => setInvoicing({ ...invoicing, invoicePrefix: v })} placeholder="INV-" />
              </div>
              <div>
                <FieldLabel label="Currency" htmlFor="currency" />
                <TextInput id="currency" value={invoicing.currency} onChange={(v) => setInvoicing({ ...invoicing, currency: v })} placeholder="SAR" />
              </div>
              <div className="space-y-3 pt-2">
                <Toggle id="autoGenerate" checked={invoicing.autoGenerate} onChange={(v) => setInvoicing({ ...invoicing, autoGenerate: v })} label="Auto-generate invoices" />
                <Toggle id="includeDamages" checked={invoicing.includeDamages} onChange={(v) => setInvoicing({ ...invoicing, includeDamages: v })} label="Include damages in invoice" />
                <Toggle id="includeViolations" checked={invoicing.includeViolations} onChange={(v) => setInvoicing({ ...invoicing, includeViolations: v })} label="Include violations in invoice" />
              </div>
            </div>
          </Card>
        </div>
      )}

      {/* ── Company Profile ─────────────────────────────────────────────────── */}
      {activeTab === 'company' && (
        <Card className="max-w-2xl p-5">
          <h3 className="mb-4 text-sm font-semibold uppercase tracking-wide text-slate-600">Company Information</h3>
          <div className="space-y-4">
            <div>
              <FieldLabel label="Company name" htmlFor="companyName" />
              <TextInput id="companyName" value={company.companyName} onChange={(v) => setCompany({ ...company, companyName: v })} />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <FieldLabel label="CR No" htmlFor="crNo" />
                <TextInput id="crNo" value={company.crNo} onChange={(v) => setCompany({ ...company, crNo: v })} />
              </div>
              <div>
                <FieldLabel label="VAT No" htmlFor="vatNo" />
                <TextInput id="vatNo" value={company.vatNo} onChange={(v) => setCompany({ ...company, vatNo: v })} />
              </div>
            </div>
            <div>
              <FieldLabel label="Address" htmlFor="address" />
              <TextInput id="address" value={company.address} onChange={(v) => setCompany({ ...company, address: v })} />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <FieldLabel label="Phone" htmlFor="phone" />
                <TextInput id="phone" value={company.phone} onChange={(v) => setCompany({ ...company, phone: v })} />
              </div>
              <div>
                <FieldLabel label="Email" htmlFor="email" />
                <TextInput id="email" value={company.email} onChange={(v) => setCompany({ ...company, email: v })} />
              </div>
            </div>
            <div>
              <FieldLabel label="Logo URL" htmlFor="logoUrl" />
              <TextInput id="logoUrl" value={company.logoUrl} onChange={(v) => setCompany({ ...company, logoUrl: v })} placeholder="https://..." />
            </div>
          </div>
        </Card>
      )}

      {/* ── Notification Rules ──────────────────────────────────────────────── */}
      {activeTab === 'notifications' && (
        <Card className="max-w-2xl p-5">
          <h3 className="mb-4 text-sm font-semibold uppercase tracking-wide text-slate-600">Email Notifications</h3>
          <div className="space-y-4">
            <Toggle id="sendInvoiceEmail" checked={notifications.sendInvoiceEmail} onChange={(v) => setNotifications({ ...notifications, sendInvoiceEmail: v })} label="Send invoice email to customer" />
            <Toggle id="sendPaymentReceipt" checked={notifications.sendPaymentReceipt} onChange={(v) => setNotifications({ ...notifications, sendPaymentReceipt: v })} label="Send payment receipt" />
            <div>
              <FieldLabel label="Overdue reminder after (days)" htmlFor="overdueDays" />
              <NumberInput id="overdueDays" value={notifications.overdueReminderDays} onChange={(v) => setNotifications({ ...notifications, overdueReminderDays: v })} min={1} max={90} />
            </div>
            <div>
              <FieldLabel label="Escalation after (days)" htmlFor="escalationDays" />
              <NumberInput id="escalationDays" value={notifications.escalationAfterDays} onChange={(v) => setNotifications({ ...notifications, escalationAfterDays: v })} min={1} max={180} />
            </div>
          </div>
        </Card>
      )}

      {/* ── Footer actions ──────────────────────────────────────────────────── */}
      <div className="flex gap-3 pt-2">
        <PrimaryButton onClick={handleSave}>
          {saved ? 'Settings Saved' : 'Save Settings'}
        </PrimaryButton>
        <SecondaryButton onClick={() => {
          setInvoicing({ ...DEFAULT_INVOICING })
          setCompany({ ...DEFAULT_COMPANY })
          setNotifications({ ...DEFAULT_NOTIFICATIONS })
        }}>
          Reset to Defaults
        </SecondaryButton>
      </div>
    </div>
  )
}
