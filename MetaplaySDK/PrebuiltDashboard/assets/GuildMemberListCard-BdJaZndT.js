import{d as b,x as S,c as w,dA as h,N as L,v as N,a8 as M,dT as T,b as k,o as d,e as m,w as i,k as D,n as s,t as a,p as C,$ as I,_ as R}from"./index-B7jvAjdE.js";import{M as n,a as r}from"./metaListUtils-B-xQ0HnD.js";import{g as O}from"./guilds-LGK3Q8Wf.js";const V=b({__name:"GuildMemberListCard",props:{guildId:{}},setup(_,{expose:c}){c();const p=_,{data:t}=S(O(p.guildId)),g=["displayName","id","role"],f=[n.asUnsorted(),new n("Role","role",r.Ascending),new n("Role","role",r.Descending),new n("Name","displayName",r.Ascending),new n("Name","displayName",r.Descending)],o=w(()=>{const l=[];for(const y of Object.keys(t.value.model.members)){const x=h(t.value.model.members[y]);x.id=y,l.push(x)}return l});function u(l){return Math.abs(L.fromISO(l).diffNow("hours").hours)<12}const e={props:p,guildData:t,searchFields:g,sortOptions:f,members:o,hasRecentlyLoggedIn:u,get cloneDeep(){return h},get DateTime(){return L},computed:w,get MetaListSortDirection(){return r},get MetaListSortOption(){return n},get MListItem(){return N},get MTextButton(){return M},get useSubscription(){return S},get guildRoleDisplayString(){return T},get getSingleGuildSubscriptionOptions(){return O}};return Object.defineProperty(e,"__isScriptSetup",{enumerable:!1,value:!0}),e}}),v={class:"tw-mr-1"},B={class:"tw-mr-1"},G={class:"tw-ml-1"},z={class:"tw-ml-1"};function A(_,c,p,t,g,f){const o=k("fa-icon"),u=k("meta-list-card");return d(),m(u,{id:"guild-member-list-card",title:"Members",icon:"users",itemList:t.members,searchFields:t.searchFields,sortOptions:t.sortOptions,emptyMessage:"This guild is empty!"},{"item-card":i(({item:e})=>[D(t.MListItem,null,{"bottom-left":i(()=>[s("span",null,"Poked: "+a(e.numTimesPoked)+" times |",1),s("span",G,"Vanity points: "+a(e.numVanityPoints)+" |",1),s("span",z,"Vanity rank: "+a(e.numVanityRanksConsumed),1)]),"top-right":i(()=>[D(t.MTextButton,{to:`/players/${e.id}`},{default:i(()=>c[0]||(c[0]=[C("View player")]),void 0,!0),_:2},1032,["to"])]),default:i(()=>[s("span",v,[e.isOnline?(d(),m(o,{key:0,class:"text-success",size:"xs",icon:"circle"})):t.hasRecentlyLoggedIn(e.lastOnlineAt)?(d(),m(o,{key:1,class:"text-success",size:"xs",icon:["far","circle"]})):(d(),m(o,{key:2,class:"text-dark",size:"xs",icon:["far","circle"]}))]),s("span",B,a(e.displayName||"n/a"),1),s("span",{class:I(e.role==="Leader"?"tw-text-orange-400 tw-text-sm":"tw-text-neutral-500 tw-text-sm")},a(t.guildRoleDisplayString(e.role)),3)],void 0,!0),_:2},1024)]),_:1},8,["itemList"])}const U=R(V,[["render",A],["__file","GuildMemberListCard.vue"]]);export{U as default};
