import{bL as Le,d as Ie,h as me,N as j,f as b,c as g,C as fe,bG as te,aU as ge,M as Be,s as Oe,a8 as Ae,L as Fe,bM as Te,bN as Ue,ao as ve,b as P,o,e as k,w as u,k as a,$ as ne,l as h,p as v,t as y,n as m,a as l,F as Y,r as J,a2 as Q,b5 as he,b6 as we,bd as Ne,_ as ze}from"./index-B7jvAjdE.js";import{M as Ee}from"./MInputMultiSelectCheckbox-D_9f7ub1.js";import{M as Ke}from"./MInputSingleSelectRadio-BukGu4ZH.js";import{M as De}from"./MInputText-CzhsT8yb.js";import{a as ie}from"./metaListUtils-B-xQ0HnD.js";import{a as oe}from"./utils-Dwlepb3M.js";import{b as Re}from"./_baseIteratee-BXhw53re.js";import{B as Ve,c as se}from"./button-group-BUOztpby.js";import"./MInputCheckbox-BqateWtE.js";import"./index-B_QkKjtG.js";import"./MInputHintMessage-DbuBS4Kn.js";import"./index-CDBqmjrS.js";import"./debounce-B8gTqdZO.js";import"./isSymbol-B7ZrWRtH.js";import"./toString-DMHXP3G8.js";import"./identity-DKeuBCMA.js";var qe=4;function ae(f){return Le(f,qe)}function He(f,n){for(var t,e=-1,D=f.length;++e<D;){var L=n(f[e]);L!==void 0&&(t=t===void 0?L:t+L)}return t}var Ge=NaN;function je(f,n){var t=f==null?0:f.length;return t?He(f,n)/t:Ge}function be(f,n){return je(f,Re(n))}const Ye=Ie({__name:"MetaListCard",props:{title:{},icon:{default:void 0},tooltip:{default:void 0},dangerous:{type:Boolean},itemList:{default:void 0},getItemKey:{type:Function,default:void 0},pageSize:{default:8},searchFields:{default:void 0},searchPlaceholder:{default:"Type your search here..."},filterSets:{default:void 0},sortOptions:{default:void 0},defaultSortOption:{default:0},emptyMessage:{default:"Nothing to list!"},noResultsMessage:{default:"No items found. Try a different search string or filters? 🤔"},moreInfoUri:{default:void 0},moreInfoLabel:{default:"relevant"},moreInfoPermission:{default:void 0},listLayout:{default:"list"},clickable:{type:Boolean,default:!1},permission:{default:void 0},description:{default:void 0},allowPausing:{type:Boolean,default:!1}},setup(f,{expose:n}){n();const t=f,e=ge(),D=ve;me(()=>{if(setTimeout(I,0),t.filterSets){const i=t.filterSets.map(c=>c.key),s=Array.from(new Set(i));i.length!==s.length&&console.warn(`Duplicate filter keys found in MetaListCard '${t.title}'`)}t.filterSets&&t.filterSets.forEach(i=>{i.filterOptions.forEach(s=>{s.initiallyActive&&x.value.push(E(i,s))})}),N.value=t.defaultSortOption,S.value=ae(t.itemList)??[],G.value=!1,ee.value=j.now()});const L=b([]),p=b(10);function I(){const i=be(L.value,s=>s.clientHeight);i>p.value&&(p.value=i)}const T=g(()=>t.permission?e.doesHavePermission(t.permission):!0),C=g(()=>(S.value?.length??0)>0),R=g(()=>(t.searchFields&&t.searchFields.length>0)??(t.sortOptions&&t.sortOptions.length>1)??!!(t.filterSets&&t.filterSets.length>0)),B=b(!1);function O(){B.value=!B.value}const U=b("");function r(i){return i.filterOptions.map(s=>{const c=E(i,s);return{label:`${s.displayName} (${X([c]).length})`,value:c}})}const x=b([]),N=b(0),z=g(()=>{let i=X(x.value);if(t.sortOptions){const s=t.sortOptions[N.value],c=s.sortKey;let M;c===null?M=w=>0:typeof c=="string"?M=w=>{let d=c?oe(w,c):w;return typeof d=="string"&&(d=d.toLowerCase()),d}:typeof c=="function"&&(M=w=>{let d=c(w);return typeof d=="string"&&(d=d.toLowerCase()),d});const A=s.direction;i=[...i].sort((w,d)=>{let _=0;const F=M(w),ce=M(d);return F>ce?_=1:F<ce&&(_=-1),_=_*A,_})}return i}),ye=g(()=>t.sortOptions?t.sortOptions.map((i,s)=>({value:s,label:i.displayName+(i.direction===ie.Descending?" ↓":i.direction===ie.Ascending?" ↑":"")})):[]),V=g(()=>U.value.length>0),W=g(()=>x.value.length>0||V.value),le=g(()=>C.value&&t.sortOptions!==void 0),_e=g(()=>W.value||le.value);function E(i,s){return`${i.key}-${s.displayName}`}function re(i,s){return i.filterOptions.some(c=>s.includes(E(i,c)))}function X(i){let s=S.value??[];const c=t.filterSets;c&&(s=s.filter(A=>{let w=!0;return c.forEach(d=>{if(re(d,i)){let _=!1;d.filterOptions.forEach(F=>{i.includes(E(d,F))&&F.filterFn(A)&&(_=!0)}),_||(w=!1)}}),w}));const M=t.searchFields;if(V.value&&M){const A=U.value.toLowerCase();s=s.filter(w=>M.some(d=>{if(d){const _=oe(w,d);return _===void 0?(Z.value.includes(d)||(console.warn(`Could not find search field '${d}' inside item for meta-list-card '${t.title}'`),Z.value.push(d)),!1):String(_).toLowerCase().includes(A)}else return String(w).toLowerCase().includes(A)}))}return s}const Z=b([]),pe=g(()=>{const i=S.value?.length??0;return V.value||W.value?`${z.value.length||0} / ${i}`:`${i}`}),xe=g(()=>(S.value?.length??0)===0?"neutral":t.dangerous?"danger":"primary"),q=b(0),Me=g(()=>{if(de.value){const i=$.value*t.pageSize;return z.value.slice(i,i+t.pageSize)}else return z.value}),de=g(()=>K.value>1),K=g(()=>{const i=z.value.length;return Math.ceil(i/t.pageSize)}),$=g(()=>Math.min(q.value,K.value-1));function ke(i){q.value=se(i,0,K.value-1),I()}function Se(i){q.value=se($.value+i,0,K.value-1),I()}const H=b(!1),G=b(!1),ee=b(j.now());function Ce(){H.value=!H.value}const S=b();fe([()=>t.itemList,H],([i,s],[c,M])=>{s?M&&(te(S.value,i)||(G.value=!0)):(S.value=ae(i)??[],G.value=!1,ee.value=j.now())});function Pe(i){const s=S.value?.findIndex(c=>te(c,i))??-1;return console.assert(s>=0,"Cannot find index for item that cannot be found in the list."),s}const ue={props:t,permissions:e,uniqueKeyPrefix:D,itemsElements:L,emptyListItemHeight:p,updateEmptyLineHeight:I,hasPermission:T,hasContent:C,showUtilitiesMenu:R,utilitiesMenuExpanded:B,toggleUtilitiesMenu:O,searchString:U,getFilterSetOptions:r,selectedFilters:x,sortOption:N,searchedFilteredSortedItemList:z,radioSortOptions:ye,searchActive:V,filtersActive:W,sortActive:le,filtersOrSortActive:_e,makeFilterKey:E,isFilterSetActive:re,getSearchFilteredItemList:X,warnedMissingSearchFields:Z,itemCountText:pe,itemCountClass:xe,desiredCurrentPage:q,currentPageItemList:Me,showPagingControls:de,totalPageCount:K,currentPage:$,gotoPageAbsolute:ke,gotoPageRelative:Se,isPaused:H,updateAvailable:G,lastUpdated:ee,togglePlayPauseState:Ce,deferredItemList:S,getItemIndex:Pe,get BButtonGroup(){return Ve},get clamp(){return se},get clone(){return ae},get isEqual(){return te},get meanBy(){return be},get DateTime(){return j},computed:g,onMounted:me,ref:b,watch:fe,get MBadge(){return Be},get MInputMultiSelectCheckbox(){return Ee},get MInputSingleSelectRadio(){return Ke},get MInputText(){return De},get MList(){return Oe},get MTextButton(){return Ae},get MTooltip(){return Fe},get MTransitionCollapse(){return Te},get MIconButton(){return Ue},get usePermissions(){return ge},get makeIntoUniqueKey(){return ve},get MetaListSortDirection(){return ie},get resolve(){return oe}};return Object.defineProperty(ue,"__isScriptSetup",{enumerable:!1,value:!0}),ue}}),Je={style:{"margin-top":"-1rem"}},Qe={key:0,class:"text-muted mr-2 tw-italic"},We={key:1,class:"text-muted mr-2 tw-italic"},Xe={key:2,class:"text-muted mr-2 tw-italic"},Ze={key:0,class:"mb-4 pt-4 small text-muted tw-text-center"},$e={key:1,class:"card-manual-content-padding pt-2 tw-w-full"},et={key:2,class:"d-flex flex-column justify-content-between h-100"},tt={key:0,class:"bg-light card-manual-content-padding border-top border-bottom tw-w-full"},nt={class:"mb-3 tw-mt-4"},it={class:"mb-2"},ot={key:1,class:"tw-relative tw-bottom-1 tw-text-xs+ tw-text-neutral-500"},st={key:1,class:"tw-relative tw-bottom-1 tw-text-xs+ tw-text-neutral-500"},at={key:1,class:"tw-relative tw-bottom-1 tw-text-xs+ tw-text-neutral-500"},lt={key:0,class:"small card-manual-content-padding"},rt={class:"small card-manual-content-padding"},dt={key:1,class:"tw-mt-1 tw-space-x-1 tw-pb-2 tw-text-center tw-text-xs+ tw-text-neutral-500"},ut={key:2},ct={key:1,class:"d-flex flex-wrap card-manual-content-padding tw-my-4"},mt={style:{"font-size":"0.7rem"}},ft={class:"small text-muted","data-testid":"meta-list-card-no-results-message"},gt={key:0,class:"tw-mb-4 tw-mt-4"},vt={key:0,class:"mb-2 tw-w-full tw-text-center"},ht={class:"small text-muted"},wt={key:1,class:"tw-mb-1 tw-text-center"},bt={class:"px-3 bg-secondary text-light pagination-shadow",style:{"padding-top":"0.3rem","min-width":"4rem"}},yt={key:3},_t={key:0,class:"small card-manual-content-padding"},pt={class:"mb-2"},xt={class:"pb-4 my-5 text-muted tw-px-4 tw-text-center"};function Mt(f,n,t,e,D,L){const p=P("fa-icon"),I=P("b-card-title"),T=P("b-row"),C=P("b-skeleton"),R=P("b-col"),B=P("meta-time"),O=P("b-button"),U=P("b-card");return o(),k(U,{class:ne(["shadow-sm",{"bg-light":!e.hasContent||!e.hasPermission}]),style:{"min-height":"12rem"},"no-body":""},{default:u(()=>[a(T,{class:ne(["card-manual-title-padding",{"table-row-link":e.hasContent&&e.showUtilitiesMenu}]),"align-h":"between","align-v":"center","no-gutters":"",onClick:e.toggleUtilitiesMenu},{default:u(()=>[a(e.MTooltip,{content:t.tooltip,"no-underline":""},{default:u(()=>[a(I,{class:"d-flex align-items-center"},{default:u(()=>[t.icon?(o(),k(p,{key:0,class:"mr-2",icon:t.icon},null,8,["icon"])):h("",!0),v(y(t.title),1),t.itemList&&e.hasPermission?(o(),k(e.MBadge,{key:1,class:"tw-ml-1",shape:"pill",variant:e.itemCountClass},{default:u(()=>[v(y(e.itemCountText),1)],void 0,!0),_:1},8,["variant"])):h("",!0)],void 0,!0),_:1})],void 0,!0),_:1},8,["content"]),m("span",Je,[e.filtersActive&&e.sortActive?(o(),l("small",Qe,"filtered & sorted")):e.filtersActive?(o(),l("small",We,"filtered")):e.sortActive?(o(),l("small",Xe,"sorted")):h("",!0),e.showUtilitiesMenu?(o(),k(e.MIconButton,{key:3,"disabled-tooltip":e.hasContent?void 0:"There is nothing to search, filter or sort.",variant:e.filtersOrSortActive?"primary":"neutral","aria-label":"Toggle the utilities menu",onClick:e.toggleUtilitiesMenu},{default:u(()=>[a(p,{class:"tw-mr-1",icon:"angle-right",size:"sm"}),a(p,{icon:"search",size:"sm"})],void 0,!0),_:1},8,["disabled-tooltip","variant"])):h("",!0)])],void 0,!0),_:1},8,["class"]),e.hasPermission?t.itemList?e.hasContent?(o(),l("div",et,[m("div",null,[a(e.MTransitionCollapse,null,{default:u(()=>[e.showUtilitiesMenu&&e.utilitiesMenuExpanded?(o(),l("div",tt,[m("div",nt,[m("div",it,[n[9]||(n[9]=m("div",{class:"tw-mb-1 tw-text-sm tw-font-semibold"},"Search",-1)),t.searchFields?(o(),k(e.MInputText,{key:0,"model-value":e.searchString,placeholder:t.searchPlaceholder,variant:e.searchActive?"success":"default",debounce:200,showClearButton:"","onUpdate:modelValue":n[0]||(n[0]=r=>e.searchString=r)},null,8,["model-value","placeholder","variant"])):(o(),l("span",ot,"Search not available for this card."))]),a(T,null,{default:u(()=>[a(R,null,{default:u(()=>[n[10]||(n[10]=m("div",{class:"tw-mb-1 tw-text-sm tw-font-semibold"},"Filter",-1)),t.filterSets?(o(!0),l(Y,{key:0},J(t.filterSets,(r,x)=>(o(),k(e.MInputMultiSelectCheckbox,{key:x,"model-value":e.selectedFilters,options:e.getFilterSetOptions(r),size:"small",class:ne(["tw-mb-0.5",{"tw-text-neutral-500":!e.isFilterSetActive(r,e.selectedFilters)}]),"onUpdate:modelValue":n[1]||(n[1]=N=>e.selectedFilters=N)},null,8,["model-value","options","class"]))),128)):(o(),l("span",st,"Filters not available for this card."))],void 0,!0),_:1}),a(R,null,{default:u(()=>[n[11]||(n[11]=m("div",{class:"tw-mb-1 tw-text-sm tw-font-semibold"},"Sort",-1)),t.sortOptions?(o(),k(e.MInputSingleSelectRadio,{key:0,"model-value":e.sortOption,options:e.radioSortOptions,size:"small","onUpdate:modelValue":n[2]||(n[2]=r=>e.sortOption=r)},null,8,["model-value","options"])):(o(),l("span",at,"Sorting not available for this card."))],void 0,!0),_:1})],void 0,!0),_:1})])])):h("",!0)],void 0,!0),_:1}),t.description?(o(),l("div",lt,y(t.description),1)):h("",!0),m("div",rt,[Q(f.$slots,"description",{},void 0,!0)]),t.allowPausing?(o(),l("div",dt,[he(m("span",null,[n[12]||(n[12]=v("Last update ")),a(B,{date:e.lastUpdated},null,8,["date"]),n[13]||(n[13]=v("."))],512),[[we,!e.isPaused]]),he(m("span",null,[n[14]||(n[14]=v("Updates paused ")),a(B,{date:e.lastUpdated},null,8,["date"]),n[15]||(n[15]=v("."))],512),[[we,e.isPaused]]),a(e.MTextButton,{onClick:e.togglePlayPauseState,"data-testid":"play-pause-button"},{default:u(()=>[v(y(e.isPaused?"Resume":"Pause")+" updates",1)],void 0,!0),_:1}),m("div",null,[e.updateAvailable?(o(),k(e.MBadge,{key:0,shape:"pill",variant:"primary"},{default:u(()=>n[16]||(n[16]=[v("New data available")]),void 0,!0),_:1})):h("",!0)])])):h("",!0),e.currentPageItemList.length>0?(o(),l("div",ut,[t.listLayout==="list"?(o(),k(e.MList,{key:0,"data-testid":"meta-list-card-list-container"},{default:u(()=>[(o(!0),l(Y,null,J(e.currentPageItemList,(r,x)=>(o(),l("div",{key:t.getItemKey?t.getItemKey(r):x,ref_for:!0,ref:"itemsElements"},[Q(f.$slots,"item-card",{item:r,index:e.getItemIndex(r)},()=>[v(y(r),1)],!0)]))),128)),e.currentPage>0?(o(!0),l(Y,{key:0},J(t.pageSize-e.currentPageItemList.length,r=>(o(),l("div",{key:e.uniqueKeyPrefix+r.toString(),style:Ne(`min-height: ${e.emptyListItemHeight}px`)},null,4))),128)):h("",!0)],void 0,!0),_:3})):t.listLayout==="flex"?(o(),l("div",ct,[(o(!0),l(Y,null,J(e.currentPageItemList,(r,x)=>(o(),l("div",{class:"mr-2 mb-2",key:x},[Q(f.$slots,"item-card",{item:r,index:e.getItemIndex(r)},()=>[m("pre",mt,y(r),1)],!0)]))),128))])):h("",!0)])):(o(),k(T,{key:3,class:"mb-4 pt-4 tw-text-center","align-h":"center","no-gutters":""},{default:u(()=>[m("span",ft,y(t.noResultsMessage),1)],void 0,!0),_:1}))]),t.moreInfoUri||e.showPagingControls?(o(),l("div",gt,[t.moreInfoUri?(o(),l("div",vt,[m("span",ht,[n[17]||(n[17]=v("View more items at the ")),a(e.MTextButton,{to:t.moreInfoUri,permission:t.moreInfoPermission},{default:u(()=>[v(y(t.moreInfoLabel)+" page",1)],void 0,!0),_:1},8,["to","permission"]),n[18]||(n[18]=v("!"))])])):h("",!0),e.showPagingControls?(o(),l("div",wt,[a(e.BButtonGroup,{size:"sm"},{default:u(()=>[a(O,{disabled:e.currentPage==0,onClick:n[3]||(n[3]=r=>e.gotoPageAbsolute(0))},{default:u(()=>[a(p,{icon:["fas","fast-backward"]})],void 0,!0),_:1},8,["disabled"]),a(O,{disabled:e.currentPage==0,onClick:n[4]||(n[4]=r=>e.gotoPageRelative(-1))},{default:u(()=>[a(p,{icon:"backward"})],void 0,!0),_:1},8,["disabled"]),m("div",bt,[m("small",null,y(e.currentPage+1)+" / "+y(e.totalPageCount),1)]),a(O,{disabled:e.currentPage==e.totalPageCount-1,onClick:n[5]||(n[5]=r=>e.gotoPageRelative(1))},{default:u(()=>[a(p,{icon:"forward"})],void 0,!0),_:1},8,["disabled"]),a(O,{disabled:e.currentPage==e.totalPageCount-1,onClick:n[6]||(n[6]=r=>e.gotoPageAbsolute(e.totalPageCount-1))},{default:u(()=>[a(p,{icon:["fas","fast-forward"]})],void 0,!0),_:1},8,["disabled"])],void 0,!0),_:1})])):h("",!0)])):h("",!0)])):(o(),l("div",yt,[t.description?(o(),l("div",_t,y(t.description),1)):h("",!0),m("div",pt,[Q(f.$slots,"description",{},void 0,!0)]),m("div",xt,y(t.emptyMessage),1)])):(o(),l("div",$e,[a(C,{width:"85%"}),a(C,{width:"55%"}),a(C,{width:"70%"}),a(C,{class:"tw-mt-4",width:"80%"}),a(C,{width:"65%"})])):(o(),l("div",Ze,[n[7]||(n[7]=v("You need the ")),a(e.MBadge,null,{default:u(()=>[v(y(t.permission),1)],void 0,!0),_:1}),n[8]||(n[8]=v(" permission to view this card."))]))],void 0,!0),_:3},8,["class"])}const Dt=ze(Ye,[["render",Mt],["__scopeId","data-v-4559f8b9"],["__file","MetaListCard.vue"]]);export{Dt as default};
