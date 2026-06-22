'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type BranchDto } from '../../lib/bff-client'
import { ErrorBox } from '../../components/ui'
import {
  type Column,
  DataGrid,
  DetailPanel,
  DetailRow,
  DetailSection,
  FilterBar,
  FilterPill,
  PageShell,
  SearchBox,
  StatusBadge,
} from '../../components/data-grid'

export default function BranchesPage() {
  const { locale } = useLocale()
  const router = useRouter()
  const [search, setSearch] = useState('')
  const [activeFilter, setActiveFilter] = useState('')
  const [data, setData] = useState<BranchDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [selected, setSelected] = useState<BranchDto | null>(null)

  async function load() {
    setError(null)
    setLoading(true)
    try { setData(await bff.getBranches()) }
    catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => { load() }, [])

  const filtered = (data ?? []).filter((b) => {
    if (activeFilter === '1' && !b.isActive) return false
    if (activeFilter === '0' && b.isActive) return false
    if (!search) return true
    const s = search.toLowerCase()
    return b.code.toLowerCase().includes(s) || b.nameEn.toLowerCase().includes(s) || b.nameAr.toLowerCase().includes(s) || (b.city ?? '').toLowerCase().includes(s)
  })

  const branchName = (b: BranchDto) => locale === 'ar' ? b.nameAr : b.nameEn

  const columns: Column<BranchDto>[] = [
    { key: 'code', header: 'Code', render: (b) => <span className="font-mono font-semibold text-slate-900">{b.code}</span> },
    { key: 'name', header: 'Branch Name', render: (b) => <span className="font-medium">{branchName(b)}</span> },
    { key: 'nameAr', header: 'Arabic Name', render: (b) => <span className="text-slate-600">{b.nameAr}</span> },
    { key: 'city', header: 'City', render: (b) => b.city ?? '—' },
    { key: 'active', header: 'Status', render: (b) => <StatusBadge tone={b.isActive ? 'green' : 'slate'}>{b.isActive ? 'Active' : 'Inactive'}</StatusBadge> },
  ]

  return (
    <PageShell
      title="Branches"
      subtitle={`${filtered.length} branches`}
      actions={
        <button onClick={() => router.push('/branches/new')} className="rounded-md bg-brand-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-800">
          + Add Branch
        </button>
      }
    >
      <FilterBar>
        <SearchBox value={search} onChange={setSearch} placeholder="Search branches..." />
        <FilterPill value={activeFilter} onChange={setActiveFilter} options={[{ value: '1', label: 'Active' }, { value: '0', label: 'Inactive' }]} placeholder="All" />
      </FilterBar>

      {error && <div className="p-4"><ErrorBox message={error} onRetry={load} /></div>}

      <div className="flex">
        <div className={`flex-1 ${selected ? 'max-w-[calc(100%-400px)]' : ''}`}>
          <DataGrid
            columns={columns}
            rows={filtered}
            totalCount={filtered.length}
            page={1}
            pageSize={filtered.length || 1}
            onPageChange={() => {}}
            onRowClick={setSelected}
            selectedId={selected?.id ?? null}
            loading={loading}
          />
        </div>

        <DetailPanel
          open={!!selected}
          onClose={() => setSelected(null)}
          title={selected ? branchName(selected) : ''}
          {...(selected ? { subtitle: selected.code } : {})}
          {...(selected ? { badge: <StatusBadge tone={selected.isActive ? 'green' : 'slate'}>{selected.isActive ? 'Active' : 'Inactive'}</StatusBadge> } : {})}
        >
          {selected && (
            <>
              <DetailSection title="Details">
                <DetailRow label="Code" value={selected.code} />
                <DetailRow label="Name (EN)" value={selected.nameEn} />
                <DetailRow label="Name (AR)" value={selected.nameAr} />
                <DetailRow label="City" value={selected.city ?? '—'} />
                <DetailRow label="Active" value={selected.isActive ? 'Yes' : 'No'} />
              </DetailSection>
              <DetailSection title="Actions">
                <button
                  onClick={() => router.push(`/branches/${selected.id}`)}
                  className="w-full rounded-md bg-brand-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-800"
                >
                  View / Edit Branch
                </button>
              </DetailSection>
            </>
          )}
        </DetailPanel>
      </div>
    </PageShell>
  )
}
