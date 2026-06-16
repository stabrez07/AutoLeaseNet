'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type BranchDto } from '../../lib/bff-client'
import {
  Badge,
  DataTable,
  ErrorBox,
  FilterSelect,
  PageHeader,
  PrimaryButton,
  SearchInput,
  SecondaryButton,
  Spinner,
  TableCell,
  TableHeadCell,
  Toolbar,
  ToolbarGroup,
} from '../../components/ui'

export default function BranchesPage() {
  const { t, locale } = useLocale()
  const router = useRouter()
  const [search, setSearch] = useState('')
  const [activeFilter, setActiveFilter] = useState<number | ''>('')
  const [data, setData] = useState<BranchDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function load() {
    setError(null)
    setLoading(true)
    try {
      setData(await bff.getBranches())
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  const filtered = data?.filter((b) => {
    if (activeFilter === 1 && !b.isActive) return false
    if (activeFilter === 0 && b.isActive) return false
    if (!search) return true
    const s = search.toLowerCase()
    return b.code.toLowerCase().includes(s) || b.nameEn.toLowerCase().includes(s) || b.nameAr.toLowerCase().includes(s)
  }) ?? []

  return (
    <div className="space-y-4">
      <PageHeader
        title={t.branches.title}
        subtitle={t.branches.subtitle}
        action={
          <PrimaryButton onClick={() => router.push('/branches/new')}>
            + {t.crudBranches.newTitle}
          </PrimaryButton>
        }
      />
      <Toolbar>
        <ToolbarGroup>
          <SearchInput value={search} onChange={setSearch} placeholder="Search branches…" className="md:w-96" />
          <FilterSelect value={activeFilter} onChange={setActiveFilter}>
            <option value="">— {t.branches.columns.active} —</option>
            <option value={1}>{t.branches.yes}</option>
            <option value={0}>{t.branches.no}</option>
          </FilterSelect>
        </ToolbarGroup>
        <div className="text-xs text-slate-500">{t.table.total}: {filtered.length}</div>
      </Toolbar>

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {loading && <Spinner label={t.common.loading} />}

      {!loading && data && (
        <DataTable>
          <table className="w-full text-sm">
            <thead className="border-b border-slate-200 bg-white">
              <tr>
                <TableHeadCell>{t.branches.columns.code}</TableHeadCell>
                <TableHeadCell>{t.branches.columns.name}</TableHeadCell>
                <TableHeadCell>{t.branches.columns.city}</TableHeadCell>
                <TableHeadCell>{t.branches.columns.active}</TableHeadCell>
                <TableHeadCell>{t.common.actions}</TableHeadCell>
              </tr>
            </thead>
            <tbody>
              {filtered.length === 0 && (
                <tr><td colSpan={5} className="px-3 py-8 text-center text-slate-500">No branches found.</td></tr>
              )}
              {filtered.map((b) => (
                <tr key={b.id} className="cursor-pointer border-t border-slate-100 transition hover:bg-brand-50/60"
                  onClick={() => router.push(`/branches/${b.id}`)}>
                  <TableCell className="font-mono text-xs font-semibold">{b.code}</TableCell>
                  <TableCell>{locale === 'ar' ? b.nameAr : b.nameEn}</TableCell>
                  <TableCell>{b.city ?? '—'}</TableCell>
                  <TableCell>
                    <Badge tone={b.isActive ? 'green' : 'slate'}>
                      {b.isActive ? t.branches.yes : t.branches.no}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <SecondaryButton
                      onClick={(e) => { e.stopPropagation(); router.push(`/branches/${b.id}`) }}
                      className="px-2 py-1 text-xs"
                    >
                      {t.common.viewDetails}
                    </SecondaryButton>
                  </TableCell>
                </tr>
              ))}
            </tbody>
          </table>
        </DataTable>
      )}
    </div>
  )
}
